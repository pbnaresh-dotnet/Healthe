using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HealthApp.Application.Abstractions;
using HealthApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace HealthApp.Api.Controllers;

[ApiController]
[Route("api/saas-billing")]
[Authorize(Roles = "SuperAdmin,AreaManager")]
public sealed class SaaSBillingController(HealthAppDbContext db, IConfiguration configuration, IHttpClientFactory httpClientFactory, IEmailService email) : ControllerBase
{
    private Guid? ActorId => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out var id) ? id : null;
    private bool IsSuperAdmin => User.IsInRole("SuperAdmin");

    [HttpGet("invoices")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? period, CancellationToken ct)
    {
        var outletScope = IsSuperAdmin ? "" : " AND EXISTS (SELECT 1 FROM dbo.AreaManagerOutletAssignments a WHERE a.OutletId=i.OutletId AND a.AreaManagerUserId=@actor)";
        var sql = $@"SELECT i.Id,i.InvoiceNumber,i.OutletId,o.Name OutletName,o.City,i.BillingPeriod,i.IssueDateUtc,i.DueDateUtc,
i.Currency,i.Subtotal,i.DiscountAmount,i.TaxAmount,i.TotalAmount,i.AmountPaid,i.BalanceDue,CASE WHEN i.Status IN ('Issued','PartiallyPaid') AND i.DueDateUtc<SYSUTCDATETIME() THEN 'Overdue' ELSE i.Status END,i.CreatedByUserId,i.CreatedAtUtc
FROM dbo.SaaSInvoices i JOIN dbo.Outlets o ON o.Id=i.OutletId WHERE (@status IS NULL OR (CASE WHEN i.Status IN ('Issued','PartiallyPaid') AND i.DueDateUtc< SYSUTCDATETIME() THEN 'Overdue' ELSE i.Status END)=@status)
AND (@period IS NULL OR i.BillingPeriod=@period){outletScope} ORDER BY i.DueDateUtc DESC,i.CreatedAtUtc DESC";
        await using var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand(); cmd.CommandText=sql;
        Add(cmd,"@status",string.IsNullOrWhiteSpace(status)?DBNull.Value:status);
        Add(cmd,"@period",string.IsNullOrWhiteSpace(period)?DBNull.Value:period);
        if (!IsSuperAdmin) Add(cmd,"@actor",ActorId ?? Guid.Empty);
        var rows=new List<object>();
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) rows.Add(new {
            id=reader.GetGuid(0), invoiceNumber=reader.GetString(1), outletId=reader.GetGuid(2), outletName=reader.GetString(3),
            city=reader.GetString(4), billingPeriod=reader.GetString(5), issueDateUtc=reader.GetDateTime(6), dueDateUtc=reader.GetDateTime(7),
            currency=reader.GetString(8), subtotal=reader.GetDecimal(9), discountAmount=reader.GetDecimal(10), taxAmount=reader.GetDecimal(11),
            totalAmount=reader.GetDecimal(12), amountPaid=reader.GetDecimal(13), balanceDue=reader.GetDecimal(14), status=reader.GetString(15),
            createdByUserId=reader.GetGuid(16), createdAtUtc=reader.GetDateTime(17)
        });
        return Ok(rows);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GenerateSaaSInvoicesRequest request, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.BillingPeriod ?? "", @"^\d{4}-(0[1-9]|1[0-2])$"))
            return BadRequest(new { message="Billing period must be YYYY-MM." });
        if (request.DueDay is < 1 or > 28) return BadRequest(new { message="Due day must be between 1 and 28." });
        var period=request.BillingPeriod!;
        var start=DateTime.SpecifyKind(DateTime.ParseExact(period+"-01","yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),DateTimeKind.Utc);
        var next=start.AddMonths(1);
        await using var conn=db.Database.GetDbConnection();
        if(conn.State!=ConnectionState.Open) await conn.OpenAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        try {
            var scope=IsSuperAdmin ? "" : " AND EXISTS (SELECT 1 FROM dbo.AreaManagerOutletAssignments a WHERE a.OutletId=o.Id AND a.AreaManagerUserId=@actor)";
            await using var query=conn.CreateCommand(); query.Transaction=tx;
            query.CommandText=$@"SELECT o.Id,o.Name,o.City,o.State,s.Id,s.BillingCycle,s.SubscriptionFee,s.SetupFee,s.DiscountPercent,s.DiscountAmount,s.StartDate,p.Name,p.MonthlyFee,p.AnnualFee
FROM dbo.OutletSubscriptions s JOIN dbo.Outlets o ON o.Id=s.OutletId JOIN dbo.SaaSPlans p ON p.Id=s.SaaSPlanId
WHERE s.Status='Active' AND o.Status IN (1,3) AND s.StartDate<@next AND (s.BillingCycle='Monthly' OR (s.BillingCycle='SixMonths' AND DATEDIFF(month,s.StartDate,@start)%6=0) OR (s.BillingCycle='Annual' AND MONTH(s.StartDate)=MONTH(@start) AND YEAR(s.StartDate)<=YEAR(@start))){scope}";
            Add(query,"@next",next); Add(query,"@start",start);
            if(!IsSuperAdmin) Add(query,"@actor",ActorId??Guid.Empty);
            var eligible=new List<(Guid id,string name,string city,string state,Guid subscription,string cycle,decimal fee,decimal setup,decimal discountPercent,decimal discountAmount,DateTime started,string plan,decimal monthly,decimal annual)>();
            await using(var r=await query.ExecuteReaderAsync(ct)) while(await r.ReadAsync(ct)) eligible.Add((r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetGuid(4),r.GetString(5),r.GetDecimal(6),r.GetDecimal(7),r.GetDecimal(8),r.GetDecimal(9),r.GetDateTime(10),r.GetString(11),r.GetDecimal(12),r.GetDecimal(13)));
            var created=0; var skipped=0; var invoiceIds=new List<Guid>();
            foreach(var x in eligible) {
                await using var exists=conn.CreateCommand(); exists.Transaction=tx; exists.CommandText="SELECT COUNT(1) FROM dbo.SaaSInvoices WHERE OutletId=@outlet AND BillingPeriod=@period AND Status<>'Voided'";
                Add(exists,"@outlet",x.id); Add(exists,"@period",period);
                if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct))>0){skipped++;continue;}
                var amount=x.cycle switch {"Annual"=>x.annual,"SixMonths"=>x.fee,_=>x.monthly};
                var includesSetup=x.started>=start && x.started<next && x.setup>0;
                var gross=decimal.Round(amount+(includesSetup?x.setup:0m),2,MidpointRounding.AwayFromZero);
                var discount=includesSetup?Math.Min(gross,x.discountAmount):0m;
                var subtotal=Math.Max(0m,gross-discount);
                // SaaS tax is not inferred here. The configured finance/tax policy must determine the applicable tax rate.
                var configuredTaxRate=configuration.GetValue<decimal?>("Finance:SaaSBillingTaxRatePercent")??18m; var taxRate=IsSuperAdmin?(request.TaxRatePercent??configuredTaxRate):configuredTaxRate;
                if(taxRate<0||taxRate>100){await tx.RollbackAsync(ct);return BadRequest(new {message="Tax rate must be between 0 and 100."});}
                var tax=decimal.Round(subtotal*taxRate/100m,2,MidpointRounding.AwayFromZero);
                var total=decimal.Round(subtotal+tax,2,MidpointRounding.AwayFromZero);
                var id=Guid.NewGuid(); var number=$"SAAS-{start:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                var due=new DateTime(start.Year,start.Month,request.DueDay,0,0,0,DateTimeKind.Utc);
                var snapshot=JsonSerializer.Serialize(new {schemaVersion=1,invoiceNumber=number,outlet=new{id=x.id,name=x.name,city=x.city,state=x.state},subscription=new{id=x.subscription,plan=x.plan,billingCycle=x.cycle},billingPeriod=period,issueDateUtc=start,dueDateUtc=due,currency="INR",lines=new[]{new{description="SaaS subscription",amount},new{description="One-time setup fee",amount=includesSetup?x.setup:0m}},gross,discountPercent=includesSetup?x.discountPercent:0m,discountAmount=discount,taxRatePercent=taxRate,taxAmount=tax,subtotal,total,financePolicyVersion=request.FinancePolicyVersion??"SAAS-BILLING-1.0"});
                await using var insert=conn.CreateCommand();insert.Transaction=tx;
                insert.CommandText=@"INSERT dbo.SaaSInvoices(Id,InvoiceNumber,OutletId,OutletSubscriptionId,BillingPeriod,IssueDateUtc,DueDateUtc,Currency,Subtotal,DiscountAmount,TaxRatePercent,TaxAmount,TotalAmount,AmountPaid,BalanceDue,Status,SnapshotJson,SnapshotSha256,CreatedByUserId,CreatedAtUtc) VALUES(@id,@number,@outlet,@subscription,@period,@issue,@due,'INR',@subtotal,@discount,@rate,@tax,@total,0,@total,'Issued',@snapshot,@hash,@actor,@now)";
                Add(insert,"@id",id);Add(insert,"@number",number);Add(insert,"@outlet",x.id);Add(insert,"@subscription",x.subscription);Add(insert,"@period",period);Add(insert,"@issue",start);Add(insert,"@due",due);Add(insert,"@subtotal",subtotal);Add(insert,"@discount",discount);Add(insert,"@rate",taxRate);Add(insert,"@tax",tax);Add(insert,"@total",total);Add(insert,"@snapshot",snapshot);Add(insert,"@hash",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))));Add(insert,"@actor",ActorId??Guid.Empty);Add(insert,"@now",DateTime.UtcNow);
                await insert.ExecuteNonQueryAsync(ct);
                await Audit(conn,tx,id,"InvoiceIssued",new{invoiceNumber=number,total,period},ActorId,ct);
                created++;invoiceIds.Add(id);
            }
            await tx.CommitAsync(ct);
            return Ok(new{billingPeriod=period,created,skipped,invoiceIds});
        } catch { await tx.RollbackAsync(ct); throw; }
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id,CancellationToken ct)
    {
        var scope=IsSuperAdmin?"":" AND EXISTS(SELECT 1 FROM dbo.AreaManagerOutletAssignments a WHERE a.OutletId=i.OutletId AND a.AreaManagerUserId=@actor)";
        await using var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync(ct);
        await using var cmd=conn.CreateCommand();cmd.CommandText=$@"SELECT i.Id,i.InvoiceNumber,i.OutletId,o.Name,i.BillingPeriod,i.IssueDateUtc,i.DueDateUtc,i.Currency,i.Subtotal,i.DiscountAmount,i.TaxRatePercent,i.TaxAmount,i.TotalAmount,i.AmountPaid,i.BalanceDue,i.Status,i.SnapshotJson,i.SnapshotSha256 FROM dbo.SaaSInvoices i JOIN dbo.Outlets o ON o.Id=i.OutletId WHERE i.Id=@id{scope}";
        Add(cmd,"@id",id);if(!IsSuperAdmin)Add(cmd,"@actor",ActorId??Guid.Empty);
        object? invoice=null; string snapshot="";string hash="";
        await using(var r=await cmd.ExecuteReaderAsync(ct)){if(!await r.ReadAsync(ct))return NotFound();snapshot=r.GetString(16);hash=r.GetString(17);invoice=new{id=r.GetGuid(0),invoiceNumber=r.GetString(1),outletId=r.GetGuid(2),outletName=r.GetString(3),billingPeriod=r.GetString(4),issueDateUtc=r.GetDateTime(5),dueDateUtc=r.GetDateTime(6),currency=r.GetString(7),subtotal=r.GetDecimal(8),discountAmount=r.GetDecimal(9),taxRatePercent=r.GetDecimal(10),taxAmount=r.GetDecimal(11),totalAmount=r.GetDecimal(12),amountPaid=r.GetDecimal(13),balanceDue=r.GetDecimal(14),status=r.GetString(15),snapshotJson=snapshot,snapshotSha256=hash};}
        var payments=await ReadPayments(conn,id,ct);
        var audit=await ReadAudit(conn,id,ct);
        return Ok(new{invoice,payments,audit});
    }

    [HttpPost("invoices/{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid id,RecordSaaSPaymentRequest request,CancellationToken ct)
    {
        if(request.Amount<=0||request.Amount>100000000m)return BadRequest(new{message="Payment amount must be greater than zero."});
        if(!new[]{"Cash","UPI","BankTransfer","Other","OnlineReconciled"}.Contains(request.Method))return BadRequest(new{message="Choose a supported payment method."});
        if(request.Method!="Cash"&&string.IsNullOrWhiteSpace(request.Reference))return BadRequest(new{message="A transaction/reference number is required for non-cash payments."});
        if(request.Method=="OnlineReconciled"&&!request.OnlinePaymentVerified)return BadRequest(new{message="Online payment must be verified against the provider before it can be recorded as received."});
        await using var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        try{
            var scope=IsSuperAdmin?"":" AND EXISTS(SELECT 1 FROM dbo.AreaManagerOutletAssignments a WHERE a.OutletId=i.OutletId AND a.AreaManagerUserId=@actor)";
            await using var read=conn.CreateCommand();read.Transaction=tx;read.CommandText=$"SELECT i.TotalAmount,i.AmountPaid,i.Status,i.InvoiceNumber FROM dbo.SaaSInvoices i WITH (UPDLOCK,HOLDLOCK) WHERE i.Id=@id{scope}";Add(read,"@id",id);if(!IsSuperAdmin)Add(read,"@actor",ActorId??Guid.Empty);
            decimal total,paid;string status,number;await using(var r=await read.ExecuteReaderAsync(ct)){if(!await r.ReadAsync(ct))return NotFound();total=r.GetDecimal(0);paid=r.GetDecimal(1);status=r.GetString(2);number=r.GetString(3);}
            if(status=="Voided")return Conflict(new{message="Voided invoices cannot receive payments."});
            var key=request.IdempotencyKey?.Trim()??""; if(key.Length is < 8 or > 100)return BadRequest(new{message="A valid payment idempotency key is required."}); await using(var duplicate=conn.CreateCommand()){duplicate.Transaction=tx;duplicate.CommandText="SELECT Id FROM dbo.SaaSInvoicePayments WHERE InvoiceId=@invoice AND IdempotencyKey=@key";Add(duplicate,"@invoice",id);Add(duplicate,"@key",key);var existing=await duplicate.ExecuteScalarAsync(ct);if(existing is Guid existingId){await tx.CommitAsync(ct);return Ok(new{paymentId=existingId,invoiceId=id,invoiceNumber=number,amountPaid=paid,balanceDue=total-paid,status=total-paid==0?"Paid":paid>0?"PartiallyPaid":"Issued",receiptUrl=$"/api/saas-billing/payments/{existingId}/receipt",idempotentReplay=true});}} var remaining=total-paid;if(request.Amount>remaining)return BadRequest(new{message=$"Payment exceeds the outstanding balance of {remaining:0.00}."});
            var paymentId=Guid.NewGuid();var now=DateTime.UtcNow;var reference=request.Reference?.Trim()??"";
            await using var ins=conn.CreateCommand();ins.Transaction=tx;ins.CommandText=@"INSERT dbo.SaaSInvoicePayments(Id,InvoiceId,Amount,Method,Reference,Notes,ReceivedAtUtc,RecordedByUserId,Provider,ProviderVerified,IdempotencyKey,CreatedAtUtc) VALUES(@id,@invoice,@amount,@method,@reference,@notes,@received,@actor,@provider,@verified,@key,@now)";
            Add(ins,"@id",paymentId);Add(ins,"@invoice",id);Add(ins,"@amount",decimal.Round(request.Amount,2,MidpointRounding.AwayFromZero));Add(ins,"@method",request.Method);Add(ins,"@reference",reference);Add(ins,"@notes",request.Notes?.Trim()??"");Add(ins,"@received",request.ReceivedAtUtc??now);Add(ins,"@actor",ActorId??Guid.Empty);Add(ins,"@provider",request.Method=="OnlineReconciled"?(request.Provider??"Cashfree"):"Manual");Add(ins,"@verified",request.Method=="OnlineReconciled");Add(ins,"@key",key);Add(ins,"@now",now);await ins.ExecuteNonQueryAsync(ct);
            var newPaid=paid+decimal.Round(request.Amount,2,MidpointRounding.AwayFromZero);var balance=total-newPaid;var newStatus=balance==0?"Paid":"PartiallyPaid";
            await using var upd=conn.CreateCommand();upd.Transaction=tx;upd.CommandText="UPDATE dbo.SaaSInvoices SET AmountPaid=@paid,BalanceDue=@balance,Status=@status WHERE Id=@id";Add(upd,"@paid",newPaid);Add(upd,"@balance",balance);Add(upd,"@status",newStatus);Add(upd,"@id",id);await upd.ExecuteNonQueryAsync(ct);
            await Audit(conn,tx,id,"PaymentRecorded",new{paymentId,amount=request.Amount,method=request.Method,reference,notes=request.Notes,previousPaid=paid,newPaid,balance,newStatus},ActorId,ct);
            await tx.CommitAsync(ct);return Ok(new{paymentId,invoiceId=id,invoiceNumber=number,amountPaid=newPaid,balanceDue=balance,status=newStatus,receiptUrl=$"/api/saas-billing/payments/{paymentId}/receipt"});
        }catch{await tx.RollbackAsync(ct);throw;}
    }



    [HttpPost("invoices/{id:guid}/payment-link")]
    public async Task<IActionResult> GeneratePaymentLink(Guid id, [FromBody] GenerateSaaSPaymentLinkRequest request, CancellationToken ct)
    {
        var scope=IsSuperAdmin?"":" AND EXISTS(SELECT 1 FROM dbo.AreaManagerOutletAssignments a WHERE a.OutletId=i.OutletId AND a.AreaManagerUserId=@actor)";
        await using var conn=db.Database.GetDbConnection(); if(conn.State!=ConnectionState.Open)await conn.OpenAsync(ct);
        string invoiceNumber,outletName,outletEmail,city,period,currency;decimal balance;DateTime dueDate;string invoiceStatus;
        await using(var cmd=conn.CreateCommand()){
            cmd.CommandText=$@"SELECT i.InvoiceNumber,o.Name,(SELECT TOP(1) u.Email FROM dbo.Users u WHERE u.OutletId=o.Id AND u.Role=1 AND u.IsActive=1 ORDER BY u.Id),o.City,i.BillingPeriod,i.Currency,i.BalanceDue,i.DueDateUtc,i.Status FROM dbo.SaaSInvoices i JOIN dbo.Outlets o ON o.Id=i.OutletId WHERE i.Id=@id{scope}";
            Add(cmd,"@id",id);if(!IsSuperAdmin)Add(cmd,"@actor",ActorId??Guid.Empty);
            await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound(new{message="Invoice not found or not assigned to your area."});
            invoiceNumber=r.GetString(0);outletName=r.GetString(1);outletEmail=r.IsDBNull(2)?"":r.GetString(2);city=r.GetString(3);period=r.GetString(4);currency=r.GetString(5);balance=r.GetDecimal(6);dueDate=r.GetDateTime(7);invoiceStatus=r.GetString(8);
        }
        if(invoiceStatus=="Voided"||balance<=0)return Conflict(new{message="Only invoices with an outstanding balance can have a payment link."});
        if(string.IsNullOrWhiteSpace(outletEmail)||!System.Net.Mail.MailAddress.TryCreate(outletEmail,out _))return BadRequest(new{message="No valid active Outlet Admin email was found."});
        var enabled=configuration.GetValue<bool>("Cashfree:Enabled");var clientId=configuration["Cashfree:ClientId"]??"";var secret=configuration["Cashfree:ClientSecret"]??"";
        if(!enabled||string.IsNullOrWhiteSpace(clientId)||string.IsNullOrWhiteSpace(secret))return StatusCode(503,new{message="Cashfree payment links are not configured."});
        var environment=configuration["Cashfree:Environment"]??"Sandbox";var baseUrl=configuration["Cashfree:BaseUrl"];
        if(string.IsNullOrWhiteSpace(baseUrl))baseUrl=environment.Equals("Production",StringComparison.OrdinalIgnoreCase)?"https://api.cashfree.com":"https://sandbox.cashfree.com";
        var apiVersion=configuration["Cashfree:ApiVersion"]??"2025-01-01";var providerLinkId="saas-"+Guid.NewGuid().ToString("N");
        var expiry=DateTime.UtcNow.AddDays(Math.Clamp(request.ValidForDays,1,30));
        var returnUrl=configuration["Cashfree:SaaSInvoiceReturnUrl"]??"https://broccoly.in/payment";
        var notifyUrl=configuration["Cashfree:SaaSPaymentLinkWebhookUrl"]??"https://api.broccoly.in/api/saas-billing/cashfree/payment-link-webhook";
        var payload=JsonSerializer.Serialize(new{link_id=providerLinkId,link_amount=decimal.Round(balance,2,MidpointRounding.AwayFromZero),link_currency=currency,link_purpose=$"Broccoly SaaS invoice {invoiceNumber}",customer_details=new{customer_name=outletName,customer_email=outletEmail,customer_phone=""},link_notify=new{send_sms=false,send_email=false},link_meta=new{return_url=returnUrl,notify_url=notifyUrl},link_expiry_time=expiry.ToString("yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture)});
        using var message=new HttpRequestMessage(HttpMethod.Post,new Uri(new Uri(baseUrl.TrimEnd('/')+"/"),"pg/links"));
        message.Headers.TryAddWithoutValidation("x-api-version",apiVersion);message.Headers.TryAddWithoutValidation("x-client-id",clientId);message.Headers.TryAddWithoutValidation("x-client-secret",secret);message.Headers.TryAddWithoutValidation("x-idempotency-key",providerLinkId);message.Content=new StringContent(payload,Encoding.UTF8,"application/json");
        using var response=await httpClientFactory.CreateClient().SendAsync(message,ct);var responseBody=await response.Content.ReadAsStringAsync(ct);
        if(!response.IsSuccessStatusCode)return StatusCode(502,new{message="Cashfree could not create a payment link.",providerStatus=(int)response.StatusCode});
        string? paymentUrl=null;using(var doc=JsonDocument.Parse(responseBody)){if(doc.RootElement.TryGetProperty("link_url",out var u))paymentUrl=u.GetString();if(doc.RootElement.TryGetProperty("link_id",out var p))providerLinkId=p.GetString()??providerLinkId;}
        if(string.IsNullOrWhiteSpace(paymentUrl))return StatusCode(502,new{message="Cashfree did not return a payment URL."});
        var linkRecordId=Guid.NewGuid();var now=DateTime.UtcNow;
        await using(var insert=conn.CreateCommand()){insert.CommandText=@"INSERT dbo.SaaSInvoicePaymentLinks(Id,InvoiceId,Provider,ProviderLinkId,PaymentUrl,Amount,Currency,RecipientEmail,ExpiresAtUtc,Status,CreatedByUserId,CreatedAtUtc,EmailSentAtUtc,EmailError) VALUES(@id,@invoice,'Cashfree',@providerId,@url,@amount,@currency,@email,@expiry,'Created',@actor,@now,NULL,'')";
            Add(insert,"@id",linkRecordId);Add(insert,"@invoice",id);Add(insert,"@providerId",providerLinkId);Add(insert,"@url",paymentUrl);Add(insert,"@amount",balance);Add(insert,"@currency",currency);Add(insert,"@email",outletEmail);Add(insert,"@expiry",expiry);Add(insert,"@actor",ActorId??Guid.Empty);Add(insert,"@now",now);await insert.ExecuteNonQueryAsync(ct);}
        await using(var audit=conn.CreateCommand()){audit.CommandText="INSERT dbo.SaaSBillingAudit(Id,InvoiceId,Action,DetailJson,ActorUserId,OccurredAtUtc) VALUES(@id,@invoice,'PaymentLinkCreated',@detail,@actor,@at)";Add(audit,"@id",Guid.NewGuid());Add(audit,"@invoice",id);Add(audit,"@detail",JsonSerializer.Serialize(new{linkRecordId,providerLinkId,amount=balance,currency,recipient=outletEmail,expiry,sendEmail=request.SendEmail}));Add(audit,"@actor",ActorId??Guid.Empty);Add(audit,"@at",now);await audit.ExecuteNonQueryAsync(ct);}
        var emailSent=false;string? emailError=null;
        if(request.SendEmail){
            var safe=WebUtility.HtmlEncode;var html=$"<!doctype html><html><body style='font-family:Arial,sans-serif;color:#173b24;line-height:1.6'><h1>Broccoly SaaS invoice</h1><p>Hello {safe(outletName)},</p><p>Invoice <b>{safe(invoiceNumber)}</b> for {safe(period)} has an outstanding balance of <b>{safe(currency)} {balance:N2}</b>.</p><p>Outlet: {safe(outletName)} · {safe(city)}<br/>Due: {dueDate:dd MMM yyyy} UTC</p><p><a href='{safe(paymentUrl)}' style='display:inline-block;background:#14532d;color:white;text-decoration:none;padding:14px 22px;border-radius:10px'>Pay invoice securely</a></p><p>Or copy: <a href='{safe(paymentUrl)}'>{safe(paymentUrl)}</a></p><p>Link expires {expiry:dd MMM yyyy HH:mm} UTC. This is a payment request, not proof of payment.</p><p>Broccoly support · support@broccoly.in</p></body></html>";
            try{await email.SendAsync(new EmailMessage(outletEmail,$"Payment due: Broccoly SaaS invoice {invoiceNumber}",$"Invoice {invoiceNumber} for {outletName}. Amount due: {currency} {balance:N2}. Pay securely: {paymentUrl}. Expires {expiry:u}",html),ct);emailSent=true;
                await using var mark=conn.CreateCommand();mark.CommandText="UPDATE dbo.SaaSInvoicePaymentLinks SET Status='EmailSent',EmailSentAtUtc=@at WHERE Id=@id";Add(mark,"@at",DateTime.UtcNow);Add(mark,"@id",linkRecordId);await mark.ExecuteNonQueryAsync(ct);
            }catch(Exception ex){emailError=ex.Message.Length>500?ex.Message[..500]:ex.Message;await using var mark=conn.CreateCommand();mark.CommandText="UPDATE dbo.SaaSInvoicePaymentLinks SET Status='EmailFailed',EmailError=@error WHERE Id=@id";Add(mark,"@error",emailError);Add(mark,"@id",linkRecordId);await mark.ExecuteNonQueryAsync(ct);}
        }
        return Ok(new{linkRecordId,invoiceId=id,invoiceNumber,outletName,outletEmail,amount=balance,currency,paymentUrl,expiresAtUtc=expiry,emailRequested=request.SendEmail,emailSent,emailError});
    }

    [AllowAnonymous]
    [HttpPost("cashfree/payment-link-webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> CashfreePaymentLinkWebhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var bodyReader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await bodyReader.ReadToEndAsync(ct);
        var signature = Request.Headers["x-webhook-signature"].ToString();
        var timestamp = Request.Headers["x-webhook-timestamp"].ToString();
        var secret = configuration["Cashfree:ClientSecret"] ?? "";
        if (!configuration.GetValue<bool>("Cashfree:Enabled") || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp)) return Unauthorized();
        string expected;
        using (var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret))) expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(timestamp + rawBody)));
        try { if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(expected), Convert.FromBase64String(signature))) return Unauthorized(); } catch (FormatException) { return Unauthorized(); }
        string? providerLinkId=null; string? linkStatus=null; decimal? amountPaid=null; string? currency=null; string? providerPaymentId=null;
        try {
            using var doc=JsonDocument.Parse(rawBody); var root=doc.RootElement;
            var data=root.TryGetProperty("data",out var d)&&d.ValueKind==JsonValueKind.Object?d:root;
            providerLinkId=ReadJsonString(data,"link_id")??ReadJsonString(data,"cf_link_id");
            linkStatus=ReadJsonString(data,"link_status")??ReadJsonString(data,"status");
            amountPaid=ReadJsonDecimal(data,"link_amount_paid")??ReadJsonDecimal(data,"amount_paid");
            currency=ReadJsonString(data,"link_currency")??ReadJsonString(data,"currency");
            var paymentData=data.TryGetProperty("payment",out var nestedPayment)&&nestedPayment.ValueKind==JsonValueKind.Object?nestedPayment:default;
            providerPaymentId=ReadJsonString(data,"link_payment_id")??ReadJsonString(data,"cf_payment_id")??ReadJsonString(data,"payment_id")??(paymentData.ValueKind==JsonValueKind.Object?(ReadJsonString(paymentData,"cf_payment_id")??ReadJsonString(paymentData,"payment_id")):null);
        } catch(JsonException){return BadRequest(new{message="Invalid Cashfree payment-link webhook payload."});}
        if(string.IsNullOrWhiteSpace(providerLinkId))return BadRequest(new{message="Payment link ID is missing."});
        if(!string.Equals(linkStatus,"PAID",StringComparison.OrdinalIgnoreCase))return Ok(new{processed=false,status=linkStatus??"UNKNOWN"});
        if(amountPaid is null or <=0)return BadRequest(new{message="Paid amount is missing or invalid."});
        await using var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync(ct);
        await using var tx=await conn.BeginTransactionAsync(ct);
        try {
            Guid invoiceId,linkId;decimal invoiceTotal,invoicePaid;string invoiceNumber,invoiceCurrency;
            await using(var lookup=conn.CreateCommand()){
                lookup.Transaction=tx;lookup.CommandText=@"SELECT l.Id,l.InvoiceId,i.TotalAmount,i.AmountPaid,i.InvoiceNumber,i.Currency FROM dbo.SaaSInvoicePaymentLinks l WITH (UPDLOCK,HOLDLOCK) JOIN dbo.SaaSInvoices i ON i.Id=l.InvoiceId WHERE l.Provider='Cashfree' AND l.ProviderLinkId=@providerLinkId";
                Add(lookup,"@providerLinkId",providerLinkId);await using var r=await lookup.ExecuteReaderAsync(ct);
                if(!await r.ReadAsync(ct))return NotFound(new{message="Payment link is not registered."});
                linkId=r.GetGuid(0);invoiceId=r.GetGuid(1);invoiceTotal=r.GetDecimal(2);invoicePaid=r.GetDecimal(3);invoiceNumber=r.GetString(4);invoiceCurrency=r.GetString(5);
            }
            if(!string.IsNullOrWhiteSpace(currency)&&!string.Equals(currency,invoiceCurrency,StringComparison.OrdinalIgnoreCase))return Conflict(new{message="Payment-link currency does not match invoice."});
            var amount=decimal.Round(amountPaid.Value,2,MidpointRounding.AwayFromZero);
            var key="cashfree-link-"+providerLinkId;
            await using(var exists=conn.CreateCommand()){
                exists.Transaction=tx;exists.CommandText="SELECT Id FROM dbo.SaaSInvoicePayments WHERE InvoiceId=@invoice AND IdempotencyKey=@key";Add(exists,"@invoice",invoiceId);Add(exists,"@key",key);
                if(await exists.ExecuteScalarAsync(ct) is Guid existingPayment){await tx.CommitAsync(ct);return Ok(new{processed=true,duplicate=true,paymentId=existingPayment});}
            }
            if(amount>invoiceTotal-invoicePaid)return Conflict(new{message="Reported payment exceeds invoice balance; manual reconciliation is required."});
            var now=DateTime.UtcNow;var paymentId=Guid.NewGuid();
            await using(var insert=conn.CreateCommand()){
                insert.Transaction=tx;insert.CommandText=@"INSERT dbo.SaaSInvoicePayments(Id,InvoiceId,Amount,Method,Reference,Notes,ReceivedAtUtc,RecordedByUserId,Provider,ProviderVerified,IdempotencyKey,CreatedAtUtc) VALUES(@id,@invoice,@amount,'OnlineReconciled',@reference,@notes,@received,@actor,'Cashfree',1,@key,@now)";
                Add(insert,"@id",paymentId);Add(insert,"@invoice",invoiceId);Add(insert,"@amount",amount);Add(insert,"@reference",providerPaymentId??providerLinkId);Add(insert,"@notes","Verified Cashfree payment-link webhook. Provider link: "+providerLinkId);Add(insert,"@received",now);Add(insert,"@actor",Guid.Empty);Add(insert,"@key",key);Add(insert,"@now",now);await insert.ExecuteNonQueryAsync(ct);
            }
            var newPaid=invoicePaid+amount;var balance=invoiceTotal-newPaid;var status=balance==0?"Paid":"PartiallyPaid";
            await using(var update=conn.CreateCommand()){update.Transaction=tx;update.CommandText="UPDATE dbo.SaaSInvoices SET AmountPaid=@paid,BalanceDue=@balance,Status=@status WHERE Id=@id";Add(update,"@paid",newPaid);Add(update,"@balance",balance);Add(update,"@status",status);Add(update,"@id",invoiceId);await update.ExecuteNonQueryAsync(ct);}
            await using(var updateLink=conn.CreateCommand()){updateLink.Transaction=tx;updateLink.CommandText="UPDATE dbo.SaaSInvoicePaymentLinks SET Status='Paid' WHERE Id=@id";Add(updateLink,"@id",linkId);await updateLink.ExecuteNonQueryAsync(ct);}
            await Audit(conn,tx,invoiceId,"CashfreePaymentLinkPaid",new{paymentId,providerLinkId,providerPaymentId,invoiceNumber,amount,newPaid,balance},null,ct);
            await tx.CommitAsync(ct);return Ok(new{processed=true,paymentId,invoiceId,invoiceNumber,amountPaid=newPaid,balanceDue=balance,status});
        } catch { await tx.RollbackAsync(ct);throw; }
    }
    private static string? ReadJsonString(JsonElement element,string property)=>element.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static decimal? ReadJsonDecimal(JsonElement element,string property){if(!element.TryGetProperty(property,out var value))return null;if(value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number))return number;return value.ValueKind==JsonValueKind.String&&decimal.TryParse(value.GetString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var parsed)?parsed:null;}

    [HttpGet("payments/{paymentId:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid paymentId,CancellationToken ct)
    {
        await using var conn=db.Database.GetDbConnection();if(conn.State!=ConnectionState.Open)await conn.OpenAsync(ct);
        var scope=IsSuperAdmin?"":" AND EXISTS(SELECT 1 FROM dbo.AreaManagerOutletAssignments a WHERE a.OutletId=i.OutletId AND a.AreaManagerUserId=@actor)";
        await using var cmd=conn.CreateCommand();cmd.CommandText=$@"SELECT p.Id,p.Amount,p.Method,p.Reference,p.Notes,p.ReceivedAtUtc,p.ProviderVerified,i.InvoiceNumber,i.BillingPeriod,i.Currency,o.Name FROM dbo.SaaSInvoicePayments p JOIN dbo.SaaSInvoices i ON i.Id=p.InvoiceId JOIN dbo.Outlets o ON o.Id=i.OutletId WHERE p.Id=@id{scope}";
        Add(cmd,"@id",paymentId);if(!IsSuperAdmin)Add(cmd,"@actor",ActorId??Guid.Empty);
        await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return NotFound();
        string Enc(object x)=>System.Net.WebUtility.HtmlEncode(Convert.ToString(x)??"");
        var html=$"<!doctype html><html><head><meta charset='utf-8'><title>Payment receipt {Enc(r.GetString(7))}</title><style>body{{font:16px Arial;max-width:760px;margin:40px auto;padding:24px;color:#17251c}}h1{{color:#14532d}}.line{{display:flex;justify-content:space-between;border-bottom:1px solid #ddd;padding:12px 0}}@media print{{button{{display:none}}}}</style></head><body><h1>Broccoly — Payment Receipt</h1><p>Receipt reference: {Enc(r.GetGuid(0))}</p><div class='line'><span>Outlet</span><b>{Enc(r.GetString(10))}</b></div><div class='line'><span>Invoice</span><b>{Enc(r.GetString(7))}</b></div><div class='line'><span>Billing period</span><b>{Enc(r.GetString(8))}</b></div><div class='line'><span>Amount received</span><b>{Enc(r.GetString(9))} {r.GetDecimal(1):N2}</b></div><div class='line'><span>Method</span><b>{Enc(r.GetString(2))}</b></div><div class='line'><span>Reference</span><b>{Enc(r.GetString(3))}</b></div><div class='line'><span>Received at (UTC)</span><b>{Enc(r.GetDateTime(5).ToString("u"))}</b></div><div class='line'><span>Online provider verified</span><b>{r.GetBoolean(6)}</b></div><p>{Enc(r.GetString(4))}</p><button onclick='window.print()'>Print / Save PDF</button></body></html>";
        return Content(html,"text/html; charset=utf-8",Encoding.UTF8);
    }

    private static void Add(System.Data.Common.DbCommand cmd,string name,object value){var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);}
    private static async Task Audit(System.Data.Common.DbConnection conn,System.Data.Common.DbTransaction tx,Guid invoice,string action,object detail,Guid? actorId,CancellationToken ct){
        await using var cmd=conn.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT dbo.SaaSBillingAudit(Id,InvoiceId,Action,DetailJson,ActorUserId,OccurredAtUtc) VALUES(@id,@invoice,@action,@detail,@actor,@at)";
        Add(cmd,"@id",Guid.NewGuid());Add(cmd,"@invoice",invoice);Add(cmd,"@action",action);Add(cmd,"@detail",JsonSerializer.Serialize(detail));Add(cmd,"@actor",actorId??Guid.Empty);Add(cmd,"@at",DateTime.UtcNow);await cmd.ExecuteNonQueryAsync(ct);
    }
    private static async Task<List<object>> ReadPayments(System.Data.Common.DbConnection conn,Guid id,CancellationToken ct){
        await using var cmd=conn.CreateCommand();cmd.CommandText="SELECT Id,Amount,Method,Reference,Notes,ReceivedAtUtc,RecordedByUserId,Provider,ProviderVerified FROM dbo.SaaSInvoicePayments WHERE InvoiceId=@id ORDER BY ReceivedAtUtc";Add(cmd,"@id",id);var list=new List<object>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))list.Add(new{id=r.GetGuid(0),amount=r.GetDecimal(1),method=r.GetString(2),reference=r.GetString(3),notes=r.GetString(4),receivedAtUtc=r.GetDateTime(5),recordedByUserId=r.GetGuid(6),provider=r.GetString(7),providerVerified=r.GetBoolean(8),receiptUrl=$"/api/saas-billing/payments/{r.GetGuid(0)}/receipt"});return list;
    }
    private static async Task<List<object>> ReadAudit(System.Data.Common.DbConnection conn,Guid id,CancellationToken ct){
        await using var cmd=conn.CreateCommand();cmd.CommandText="SELECT Action,DetailJson,ActorUserId,OccurredAtUtc FROM dbo.SaaSBillingAudit WHERE InvoiceId=@id ORDER BY OccurredAtUtc DESC";Add(cmd,"@id",id);var list=new List<object>();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))list.Add(new{action=r.GetString(0),detailJson=r.GetString(1),actorUserId=r.GetGuid(2),occurredAtUtc=r.GetDateTime(3)});return list;
    }
}
public sealed class GenerateSaaSInvoicesRequest { public string? BillingPeriod {get;set;} public int DueDay {get;set;}=15; public decimal? TaxRatePercent {get;set;} public string? FinancePolicyVersion {get;set;} }
public sealed class RecordSaaSPaymentRequest { public decimal Amount {get;set;} public string Method {get;set;}="Cash"; public string? Reference {get;set;} public string? Notes {get;set;} public DateTime? ReceivedAtUtc {get;set;} public bool OnlinePaymentVerified {get;set;} public string? Provider {get;set;} public string? IdempotencyKey {get;set;} }

public sealed class GenerateSaaSPaymentLinkRequest { public bool SendEmail { get; set; } = true; public int ValidForDays { get; set; } = 7; }
