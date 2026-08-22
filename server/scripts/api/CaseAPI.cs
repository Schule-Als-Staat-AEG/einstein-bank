using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;
using Microsoft.AspNetCore.SignalR;

namespace Max35050.API;

public static class CaseAPI
{
    public static async Task<IResult> RequestCase(string body, string token)
    {
        // Get User from token
        if (await AuthManager.IsTokenSuperuser(token) == false)
        {
            dynamic user = await AuthManager.GetUserFromToken(token);
            if (user == null)
            {
                return Results.Unauthorized();
            }
            UserData ud = await UserManager.GetUserData(user.id);
            if (ud == null)
            {
                return Results.Unauthorized();
            }
            if (!ud.financeTrackingAccess)
            {
                return Results.Forbid();
            }
        }
        // Check if there are any free cases available
        if (!CaseManager.cases.Any(c => c.assigned == false))
        {
            return Results.Ok(new { success = false, message = "No cases available at the moment." });
        }

        Case @case = CaseManager.GetFreeCase();
        @case.assigned = true;
        dynamic userRecord = await AuthManager.GetUserFromToken(token);
        @case.handler = userRecord.id.ToString();
        dynamic result = await Server.db.Collections("assigned_cases").Create(new
        {
            transaction = @case.transaction,
            handler = @case.handler,
            taxLog = @case.taxLog,
        });
        @case.id = result.id.ToString();

        // Check if there are still free cases available after assigning this case, and broadcast the appropriate message to employees
        if (CaseManager.cases.Any(c => c.assigned == false))
        {
            await JobAPI.BroadcastNewCaseAvailable(Server.jobHubContext!);
        }
        else
        {
            await JobAPI.BroadcastNoCasesAvailable(Server.jobHubContext!);
        }

        return Results.Ok(new { success = true, caseId = @case.id });
    }
    public static async Task<IResult> SubmitCase(string body, string token)
    {
        try
        {
            // Get User from token
            if (await AuthManager.IsTokenSuperuser(token) == false)
            {
                dynamic user = await AuthManager.GetUserFromToken(token);
                if (user == null)
                {
                    return Results.Unauthorized();
                }
                UserData ud = await UserManager.GetUserData(user.id);
                if (ud == null)
                {
                    return Results.Unauthorized();
                }
                if (!ud.financeTrackingAccess)
                {
                    return Results.Forbid();
                }
            }

            dynamic? data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(body);
            if (data == null || data!.id == null || data!.illegal == null || data!.reason == null)
            {
                return Results.BadRequest(new { success = false, message = "Invalid request body." });
            }
            // Get the case object from the database using the provided ID
            Case @case = new Case
            {
                id = data!.id.ToString(),
                illegal = data.illegal.ToObject<bool>(),
                reason = data.reason.ToString() ?? "ERR:NO_REASON_TRANSMITTED"
            };
        
            dynamic caseRecord = await Server.db.Collections("assigned_cases").GetOne(data!.id.ToString());
            if (caseRecord == null)
            {
                return Results.NotFound(new { success = false, message = "Case not found." });
            }
            @case.transaction = caseRecord.transaction.ToString();
            @case.handler = caseRecord.handler.ToString();
            @case.taxLog = caseRecord.taxLog.ToString();
            @case.created = DateTime.Parse(caseRecord.created.ToString());
            @case.updated = DateTime.Parse(caseRecord.updated.ToString());

            // Update the transaction status if the case is marked as illegal
            if (@case.illegal)
            {
                await TransactionManager.SetStatus(@case.transaction, TransactionStatus.UnderAudit);
            }
            else
            {
                await TransactionManager.SetStatus(@case.transaction, TransactionStatus.Approved);
            }
            
            // Copy the case over to archived cases and delete it from assigned_cases
            dynamic archivedCaseData = new
            {
                transaction = @case.transaction,
                handler = @case.handler,
                taxLog = @case.taxLog,
                illegal = @case.illegal,
                reason = @case.reason
            };
            _ = Server.db.Collections("archived_cases").Create(archivedCaseData);
            _ = Server.db.Collections("assigned_cases").Delete(@case.id);

            // Handle Emails
            if (@case.illegal)
            {
                // Send email notification if the case is marked as illegal and email notifications are enabled
                if (SettingsManager.sendEmail && !string.IsNullOrEmpty(SettingsManager.emailReceiver))
                {
                    TransactionRecord? tr = await TransactionManager.GetTransaction(@case.transaction);
                    if (tr == null)
                    {
                        return Results.NotFound(new { success = false, message = "Transaction not found." });
                    }
                    User sender = await UserManager.GetUserByAccountID(tr.sender);
                    User receiver = await UserManager.GetUserByAccountID(tr.receiver);

                    TaxResult taxResult = await TaxManager.GetTaxResult(@case.transaction) ?? new TaxResult();

                    string subject = $"Transaktion #{@case.transaction} wurde als illegal markiert";
                    string bodyContent = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body {{ font-family: Arial, sans-serif; color: #333; background-color: #f5f5f5; }}
                            .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                            .header {{ background-color: #d32f2f; color: white; padding: 20px; border-radius: 4px; margin-bottom: 20px; text-align: center; }}
                            .header h1 {{ margin: 0; font-size: 24px; }}
                            .section {{ margin-bottom: 20px; }}
                            .section h2 {{ color: #d32f2f; font-size: 16px; margin-bottom: 10px; border-bottom: 2px solid #d32f2f; padding-bottom: 5px; }}
                            .detail-row {{ display: flex; margin-bottom: 8px; }}
                            .detail-label {{ font-weight: bold; width: 180px; color: #666; }}
                            .detail-value {{ flex-grow: 1; }}
                            .reason-box {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; border-radius: 4px; margin-bottom: 20px; }}
                            .footer {{ color: #999; font-size: 12px; text-align: center; margin-top: 20px; border-top: 1px solid #ddd; padding-top: 10px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>⚠️ Illegale Transaktion erkannt</h1>
                            </div>
                            
                            <p>Die Transaktion mit der ID <strong>#{@case.transaction}</strong> wurde von einem Mitarbeiter als illegal markiert.</p>
                            
                            <div class='reason-box'>
                                <strong>Grund:</strong><br>
                                {@case.reason}
                            </div>
                            
                            <p><strong>Bitte überprüfen Sie die Transaktion und ergreifen gegebenenfalls Maßnahmen.</strong></p>
                            
                            <div class='section'>
                                <h2>📋 Transaktionsdetails</h2>
                                <div class='detail-row'>
                                    <div class='detail-label'>Menge:</div>
                                    <div class='detail-value'>{tr.amount}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Menge (nach Steuern):</div>
                                    <div class='detail-value'>{tr.received}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Beschreibung:</div>
                                    <div class='detail-value'>{tr.description}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>System:</div>
                                    <div class='detail-value'>{tr.system}</div>
                                </div>
                            </div>
                            
                            <div class='section'>
                                <h2>👤 Senderinformationen</h2>
                                <div class='detail-row'>
                                    <div class='detail-label'>Name:</div>
                                    <div class='detail-value'>{sender.name}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Firma:</div>
                                    <div class='detail-value'>{(sender.isCompany == true ? "Ja" : "Nein")}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Firmenname:</div>
                                    <div class='detail-value'>{sender.companyName}</div>
                                </div>
                            </div>
                            
                            <div class='section'>
                                <h2>👤 Empfängerinformationen</h2>
                                <div class='detail-row'>
                                    <div class='detail-label'>Name:</div>
                                    <div class='detail-value'>{receiver.name}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Firma:</div>
                                    <div class='detail-value'>{(receiver.isCompany == true ? "Ja" : "Nein")}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Firmenname:</div>
                                    <div class='detail-value'>{receiver.companyName}</div>
                                </div>
                            </div>

                            <div class='section'>
                                <h2>🪙 Steuerinformationen</h2>
                                <div class='detail-row'>
                                    <div class='detail-label'>Einkommenssteuer bezahlt:</div>
                                    <div class='detail-value'>{taxResult.IncomeTaxAmount}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Einkommenssteuer bezahlt (an diesem Tag ohne diese Transaktion):</div>
                                    <div class='detail-value'>{taxResult.PreviousIncomeReceived}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Mehrwertsteuer bezahlt:</div>
                                    <div class='detail-value'>{taxResult.VATAmount}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Transaktionssteuer bezahlt:</div>
                                    <div class='detail-value'>{taxResult.TransactionTaxAmount}</div>
                                </div>
                                <div class='detail-row'>
                                    <div class='detail-label'>Verbleibender Steuerfreier Pool:</div>
                                    <div class='detail-value'>{taxResult.NewTaxFreePool}</div>
                                </div>
                            </div>
                            
                            <div class='footer'>
                                Dies ist eine automatisch generierte Nachricht. Bitte antworten Sie nicht auf diese E-Mail.
                            </div>
                        </div>
                    </body>
                    </html>";

                    try
                    {
                        await EmailManager.SendEmail(SettingsManager.emailReceiver, subject, bodyContent, "Finanzbehörde - Illegale Transaktion");
                    }
                    catch { }
                    
                }
            }
            CaseManager.cases.RemoveAll(c => c.id == @case.id);
            return Results.Ok(new { success = true, message = "Case submitted successfully." });
        }
        catch
        {
            return Results.InternalServerError(new { success = false, message = "Internal server error while submitting case." });
        }


        
    }
}

public class JobAPI : Hub
{
    private static readonly HashSet<string> ConnectedClients = new();
    public override async Task OnConnectedAsync()
    {
        try
        {
            var httpContext = Context.GetHttpContext();
            var token = httpContext!.Request.Query["access_token"].ToString();
            if (string.IsNullOrEmpty(token))
            {
                Context.Abort();
                return;
            }
            if (!await AuthManager.IsTokenSuperuser(token))
            {
                var user = await AuthManager.GetUserFromToken(token);
                if (user == null)
                {
                    Context.Abort();
                    return;
                }
                UserData? ud = await UserManager.GetUserData(user.id);
                if (ud == null)
                {
                    Context.Abort();
                    return;
                }
                if (!ud.financeTrackingAccess)
                {
                    Context.Abort();
                    return;
                }
            }

            ConnectedClients.Add(Context.ConnectionId);
            await Clients.Caller.SendAsync("noCasesAvailable");
            
            if(CaseManager.cases.Any(c => c.assigned == false))
            {
                await Clients.Caller.SendAsync("newCaseAvailable");
            }
            else
            {
                await Clients.Caller.SendAsync("noCasesAvailable");
            }
        }
        catch { }

        await base.OnConnectedAsync();
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedClients.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
    public static async Task BroadcastNewCaseAvailable(IHubContext<JobAPI> context)
    {
        await context.Clients.All.SendAsync("newCaseAvailable");
    }
    public static async Task BroadcastNoCasesAvailable(IHubContext<JobAPI> context)
    {
        await context.Clients.All.SendAsync("noCasesAvailable");
    }
}
