using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Exceptions;
using TexasOperationalPortal.Application.Common.Interfaces;
using TexasOperationalPortal.Contracts.Common;
using TexasOperationalPortal.Contracts.Bookings;
using TexasOperationalPortal.Contracts.Dashboard;

namespace TexasOperationalPortal.Infrastructure.Services;

public sealed class SalesOperationsClient : ISalesOperationsClient
{
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SalesOperationsClient> _logger;

    public SalesOperationsClient(
        HttpClient client, 
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SalesOperationsClient> logger)
    {
        _client = client;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        var apiKey = config["SalesPortal:ApiKey"] ?? "tx_ops_secure_888";
        _client.DefaultRequestHeaders.Add("X-OPS-KEY", apiKey);
        
        _client.Timeout = TimeSpan.FromSeconds(15);
    }

    private void AddCorrelationIdHeader()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null && context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            if (!_client.DefaultRequestHeaders.Contains("X-Correlation-ID"))
            {
                _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId.ToString());
            }
        }
    }

    private async Task<T> SendWithRetryAsync<T>(string url, HttpMethod method, CancellationToken ct) where T : class
    {
        AddCorrelationIdHeader();

        var maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(200);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var request = new HttpRequestMessage(method, url);
                _logger.LogInformation("Sending downstream request. Method: {Method}, URL: {Url}, Attempt: {Attempt}", method, url, attempt);

                var response = await _client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                    return content ?? throw new DownstreamException("Received empty response from Sales API.");
                }

                var status = (int)response.StatusCode;
                if (status == 404)
                {
                    throw new DownstreamException(404, "Requested booking detail resource not found on Sales Portal.");
                }

                if (status is 400 or 401 or 403 or 409 or 422)
                {
                    throw new DownstreamException(status, $"Sales API returned HTTP error {status}.");
                }

                if (method != HttpMethod.Get)
                {
                    throw new DownstreamException(status, $"Sales API returned server error {status} on unsafe request.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not DownstreamException)
            {
                if (attempt == maxRetries || method != HttpMethod.Get)
                {
                    var statusCode = ex switch
                    {
                        TimeoutException => 504,
                        TaskCanceledException => 504,
                        _ => 503
                    };
                    throw new DownstreamException(statusCode, "Failed to connect to Sales Portal service.", ex);
                }
            }

            if (method == HttpMethod.Get)
            {
                _logger.LogWarning("Downstream request failed. Retrying in {Delay}ms...", delay.TotalMilliseconds * attempt);
                await Task.Delay(delay * attempt, ct);
            }
            else
            {
                break;
            }
        }

        throw new DownstreamException(503, "Downstream query failed after multiple retry attempts.");
    }

    public async Task<OperationalStatusCountsResponse> GetOperationalStatusCountsAsync(CancellationToken ct)
    {
        var url = "api/internal/operations/dashboard/operational-counts";
        return await SendWithRetryAsync<OperationalStatusCountsResponse>(url, HttpMethod.Get, ct);
    }

    public async Task<PagedResponse<VisaApplicationSummaryResponse>> GetVisaApplicationsAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct)
    {
        var url = $"api/internal/operations/visas?page={page}&pageSize={pageSize}&status={status ?? ""}&search={search ?? ""}";
        return await SendWithRetryAsync<PagedResponse<VisaApplicationSummaryResponse>>(url, HttpMethod.Get, ct);
    }

    public async Task<VisaApplicationDetailResponse?> GetVisaApplicationDetailsAsync(
        long id, CancellationToken ct)
    {
        var url = $"api/internal/operations/visas/{id}";
        try
        {
            return await SendWithRetryAsync<VisaApplicationDetailResponse>(url, HttpMethod.Get, ct);
        }
        catch (DownstreamException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<PagedResponse<VfsAppointmentSummaryResponse>> GetVfsAppointmentsAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct)
    {
        var url = $"api/internal/operations/vfs-appointments?page={page}&pageSize={pageSize}&status={status ?? ""}&search={search ?? ""}";
        return await SendWithRetryAsync<PagedResponse<VfsAppointmentSummaryResponse>>(url, HttpMethod.Get, ct);
    }

    public async Task<VfsAppointmentDetailResponse?> GetVfsAppointmentDetailsAsync(
        long id, CancellationToken ct)
    {
        var url = $"api/internal/operations/vfs-appointments/{id}";
        try
        {
            return await SendWithRetryAsync<VfsAppointmentDetailResponse>(url, HttpMethod.Get, ct);
        }
        catch (DownstreamException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<PagedResponse<HolidayPackageSummaryResponse>> GetHolidayPackagesAsync(
        int page, int pageSize, string? status, string? search, CancellationToken ct)
    {
        var url = $"api/internal/operations/holiday-packages?page={page}&pageSize={pageSize}&status={status ?? ""}&search={search ?? ""}";
        return await SendWithRetryAsync<PagedResponse<HolidayPackageSummaryResponse>>(url, HttpMethod.Get, ct);
    }

    public async Task<HolidayPackageDetailResponse?> GetHolidayPackageDetailsAsync(
        long id, CancellationToken ct)
    {
        var url = $"api/internal/operations/holiday-packages/{id}";
        try
        {
            return await SendWithRetryAsync<HolidayPackageDetailResponse>(url, HttpMethod.Get, ct);
        }
        catch (DownstreamException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    private void AddFileToMultipart(MultipartFormDataContent multipart, FileStreamInput? file, string name)
    {
        if (file == null || file.Stream == null) return;
        var streamContent = new StreamContent(file.Stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        multipart.Add(streamContent, name, file.FileName);
    }

    // ─── Visa Client Actions ───

    public async Task<bool> UpdateVisaIdAsync(long firstApplicantId, string visaAppId, decimal? govtFeeAmount, string? govtFeeTransactionNo, FileStreamInput? govtFeePaymentProof, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs, long processedBy, bool isAdditive = false, CancellationToken ct = default)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applicants/{firstApplicantId}/update-visa-id";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(visaAppId), "visaAppId");
        multipart.Add(new StringContent(processedBy.ToString()), "processedBy");
        if (isAdditive)
            multipart.Add(new StringContent("true"), "isAdditive");
        if (govtFeeAmount.HasValue)
            multipart.Add(new StringContent(govtFeeAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "govtFeeAmount");
        if (govtFeeTransactionNo != null)
            multipart.Add(new StringContent(govtFeeTransactionNo), "govtFeeTransactionNo");

        AddFileToMultipart(multipart, govtFeePaymentProof, "govtFeePaymentProof");

        AddGovtFeesToMultipart(multipart, govtFees, govtFeeProofs);

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> EditVisaIdAsync(long firstApplicantId, string visaAppId, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applicants/{firstApplicantId}/edit-visa-id";
        var response = await _client.PostAsJsonAsync(url, new { visaAppId, processedBy }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ApproveVisaAsync(long id, FileStreamInput? visaDoc, string? note, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applications/{id}/approve";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(processedBy.ToString()), "processedBy");
        if (note != null) multipart.Add(new StringContent(note), "descNote");
        AddFileToMultipart(multipart, visaDoc, "visaDocument");

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RejectVisaAsync(long id, FileStreamInput? rejectionDoc, string note, string? refundNote, decimal rejectionFee, decimal? payableAmount, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applications/{id}/reject";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(processedBy.ToString()), "processedBy");
        multipart.Add(new StringContent(note ?? "Rejected by operational staff"), "descNote");
        if (refundNote != null) multipart.Add(new StringContent(refundNote), "refundNote");
        multipart.Add(new StringContent(rejectionFee.ToString(System.Globalization.CultureInfo.InvariantCulture)), "rejectionFee");
        if (payableAmount.HasValue)
            multipart.Add(new StringContent(payableAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "payableAmount");
        AddFileToMultipart(multipart, rejectionDoc, "rejectionDocument");

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RequestVisaDocumentUpdateAsync(long id, string note, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applications/{id}/request-document-update";
        var response = await _client.PostAsJsonAsync(url, new { requestNote = note, processedBy }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateVisaPayableAmountAsync(long id, decimal payableAmount, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applications/{id}/payable-amount";
        var response = await _client.PostAsJsonAsync(url, new { payableAmount }, ct);
        return response.IsSuccessStatusCode;
    }

    // ─── VFS Client Actions ───

    public async Task<bool> ProcessVfsAsync(long id, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs, long processedBy, bool isAdditive = false, CancellationToken ct = default)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/vfs-appointments/{id}/process";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(processedBy.ToString()), "processedBy");
        if (isAdditive)
            multipart.Add(new StringContent("true"), "isAdditive");

        AddGovtFeesToMultipart(multipart, govtFees, govtFeeProofs);

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateVfsScheduleAsync(long id, System.DateTime appointmentDate, string appointmentTime, string? description, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/vfs-appointments/{id}/update";
        var response = await _client.PostAsJsonAsync(url, new { appointmentDate, appointmentTime, description, processedBy }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateVfsRefAsync(long id, string vfsAppId, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/vfs-appointments/{id}/update-vfs-ref";
        var response = await _client.PostAsJsonAsync(url, new { vfsAppId, processedBy }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateVfsStatusAsync(long id, string status, FileStreamInput? vfsDoc, FileStreamInput? verifiedReceipt, FileStreamInput? rejectedReceipt, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/vfs-appointments/{id}/status";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(status), "status");
        multipart.Add(new StringContent(processedBy.ToString()), "processedBy");
        AddFileToMultipart(multipart, vfsDoc, "vfsDoc");
        AddFileToMultipart(multipart, verifiedReceipt, "verifiedReceipt");
        AddFileToMultipart(multipart, rejectedReceipt, "rejectedReceipt");

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    // ─── Holiday Client Actions ───

    public async Task<bool> ProcessHolidayAsync(long id, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/holiday-packages/{id}/process";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(processedBy.ToString()), "processedBy");

        AddGovtFeesToMultipart(multipart, govtFees, govtFeeProofs);

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateHolidayStatusAsync(long id, string status, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/holiday-packages/{id}/status";
        var response = await _client.PostAsJsonAsync(url, new { status, processedBy }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateHolidayRefAsync(long id, long processedBy, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/holiday-packages/{id}/update-holiday-id";
        var response = await _client.PostAsJsonAsync(url, new { processedBy }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UploadHolidayDocsAsync(long id, List<KeyValuePair<string, FileStreamInput>> documents, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/holiday-packages/{id}/documents";
        using var multipart = new MultipartFormDataContent();
        for (int i = 0; i < documents.Count; i++)
        {
            var kvp = documents[i];
            multipart.Add(new StringContent(kvp.Key), $"Documents[{i}].ServiceName");
            multipart.Add(new StringContent(kvp.Key), $"documents[{i}][serviceName]");
            AddFileToMultipart(multipart, kvp.Value, $"Documents[{i}].File");
        }

        var response = await _client.PostAsync(url, multipart, ct);
        return response.IsSuccessStatusCode;
    }

    private void AddGovtFeesToMultipart(MultipartFormDataContent multipart, List<KeyValuePair<string, string>> govtFees, List<FileStreamInput> govtFeeProofs)
    {
        for (int i = 0; i < govtFees.Count; i++)
        {
            var kvp = govtFees[i];
            multipart.Add(new StringContent(kvp.Value), kvp.Key);

            var match = System.Text.RegularExpressions.Regex.Match(kvp.Key, @"govt_fees\[(\d+)\]\[([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var idx = match.Groups[1].Value;
                var prop = match.Groups[2].Value.ToLower();
                string canonicalProp = prop switch
                {
                    "amount" => "Amount",
                    "supplier_id" or "supplierid" => "SupplierId",
                    "transaction_no" or "transactionno" => "TransactionNo",
                    "govt_fee_description" or "govtfeedescription" or "description" => "GovtFeeDescription",
                    "existing_payment_proof" or "existingpaymentproof" => "ExistingPaymentProof",
                    "row_key" or "rowkey" => "RowKey",
                    _ => prop
                };
                multipart.Add(new StringContent(kvp.Value), $"GovtFees[{idx}].{canonicalProp}");
            }
        }

        for (int i = 0; i < govtFeeProofs.Count; i++)
        {
            var proof = govtFeeProofs[i];
            AddFileToMultipart(multipart, proof, $"GovtFees[{i}].PaymentProof");
            AddFileToMultipart(multipart, proof, $"govt_fees[{i}][payment_proof]");
        }
    }

    public async Task<List<SupplierResponse>> GetSuppliersAsync(CancellationToken ct)
    {
        try
        {
            var directRes = await _client.GetFromJsonAsync<List<SupplierResponse>>("https://accountstexas.runasp.net/api/Supplier", ct);
            if (directRes != null && directRes.Count > 0) return directRes;
        }
        catch { }

        try
        {
            return await SendWithRetryAsync<List<SupplierResponse>>("api/internal/operations/suppliers", HttpMethod.Get, ct) ?? new List<SupplierResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch suppliers from sales service.");
            return new List<SupplierResponse>();
        }
    }

    public async Task<bool> CloseVisaOperationAsync(long id, CancellationToken ct = default)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/bookings/visa/{id}/operation-closed";
        var response = await _client.PostAsJsonAsync(url, new { operationClosed = true }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateApplicantDetailsAsync(long applicantId, UpdateVisaApplicantDetailsRequest request, CancellationToken ct)
    {
        AddCorrelationIdHeader();
        var url = $"api/internal/operations/sales-visa/applicants/{applicantId}";
        var response = await _client.PutAsJsonAsync(url, request, ct);
        return response.IsSuccessStatusCode;
    }

}