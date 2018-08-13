using System;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Xml;
using SESServices.DataTransferObjects;
using SESServices.Enumerations;
using SESServices.Models;

namespace SESServices.Controllers
{
  public class PayoffAccountsController : ControllerBase
  {
    /// <summary>
    /// Gets account information for the autocomplete function on the edit disbursement line item dialog
    /// </summary>
    /// <param name="accountNumber">The account number the user has entered</param>
    /// <returns>JSon object containing all the necessary information to fill the edit disbursement line item dialog</returns>
    [HttpGet]
    public PayoffAccountAutocomplete GetPayoffAccountByAccountNumber(string accountNumber)
    {
      try
      {
        var payoffAccount = new PayoffAccountAutocomplete();
        if (string.IsNullOrEmpty(accountNumber))
        {
          payoffAccount.Result = ResultEnum.Failure;
          payoffAccount.Message = "You must provide an account number.";
          return payoffAccount;
        }

        // if it finds an exact match, return the whole thing
        var exactMatch = _entities.PayoffBankAccounts.FirstOrDefault(a => a.Status == 0 && a.AccountNumber.Equals(accountNumber.Trim(), StringComparison.InvariantCultureIgnoreCase));
        if (exactMatch != null)
        {
          payoffAccount = new PayoffAccountAutocomplete(exactMatch)
          {
            Result = ResultEnum.Success,
            Message = "Account located."
          };

          return payoffAccount;
        }

        // if it doesn't find an exact mach, check the wildcard accounts
        var wildcardAccounts = _entities.PayoffBankAccounts.Where(a => a.Status == 0 && a.AccountNumber.Contains("X"));
        foreach (var wildcardAccount in wildcardAccounts)
        {
          // If the passed in account number does not match the length of the wildcard number, move to the next one
          if (accountNumber.Length != wildcardAccount.AccountNumber.Length) continue;

          for (var i = 0; i < accountNumber.Length; i++)
          {
            var accountChar = accountNumber[i];
            var wildcardChar = wildcardAccount.AccountNumber[i];

            // If all the leading numbers matched and we hit an 'X', it's a match
            if (wildcardChar == 'X')
            {
              payoffAccount = new PayoffAccountAutocomplete(wildcardAccount)
              {
                Result = ResultEnum.Success,
                Message = "Account located."
              };

              return payoffAccount;
            }

            // If we haven't hit an 'X' yet but the characters still match, move to the next character
            if (accountChar == wildcardChar) continue;

            // If we haven't hit an 'X' yet but the characters do not match, it isn't a matching number
            break;
          }
        }

        payoffAccount.Result = ResultEnum.Failure;
        payoffAccount.Message = "Unable to locate payoff account information with the given account number.";
        return payoffAccount;
      }
      catch (Exception ex)
      {
        // TODO: Put exception in a log file
        return new PayoffAccountAutocomplete
        {
          Result = ResultEnum.Error,
          Message = "There was a problem retreiving the payoff account information."
        };
      }

    }


    
    [HttpPost]
    public ResultMessage CreatePayoffAccount(HttpRequestMessage request)
    {
      try
      {
        using (var db = new SES_ServicesEntities())
        {
          // Perform entity permission check here

          // Check to see if the entity passing the payoff account to this can pre-approve payoff accounts

          var doc = new XmlDocument();
          doc.Load(request.Content.ReadAsStreamAsync().Result);

          if (doc.DocumentElement == null)
          {
            return new ResultMessage(ResultEnum.FailureDocumentReadError, "Unable to open xml document.");
          }



          var accountNumber = doc.DocumentElement.SelectSingleNode("accountNumber")?.InnerText;
          if (string.IsNullOrEmpty(accountNumber))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide an account number.");
          }

          var routingNumber = doc.DocumentElement.SelectSingleNode("routingNumber")?.InnerText;
          if (string.IsNullOrEmpty(routingNumber))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a routing number.");
          }

          var existingPayoffAccount = db.PayoffBankAccounts.FirstOrDefault(p => p.AccountNumber == accountNumber && p.RoutingNumber == routingNumber);
          if (existingPayoffAccount != null)
          {
            return new ResultMessage(ResultEnum.FailureDuplicateData, "Unable to create payoff account.  An account with the same account and routing numbers already exists.");
          }

          var bankName = doc.DocumentElement.SelectSingleNode("bankName")?.InnerText;
          if (string.IsNullOrEmpty(bankName))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a bank name.");
          }

          var address1 = doc.DocumentElement.SelectSingleNode("address1")?.InnerText;
          if (string.IsNullOrEmpty(address1))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide an address line one for the bank address.");
          }

          var city = doc.DocumentElement.SelectSingleNode("city")?.InnerText;
          if (string.IsNullOrEmpty(city))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a city for the bank address.");
          }

          var state = doc.DocumentElement.SelectSingleNode("state")?.InnerText;
          if (string.IsNullOrEmpty(state))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a state for the bank address.");
          }

          var zip = doc.DocumentElement.SelectSingleNode("zip")?.InnerText;
          if (string.IsNullOrEmpty(zip))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a zip code for the bank address.");
          }

          var name = doc.DocumentElement.SelectSingleNode("name")?.InnerText;
          if (string.IsNullOrEmpty(name))
          {
            return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a payee name.");
          }


          var newPayoffAccount = new PayoffBankAccount
          {
            AccountNumber = accountNumber,
            RoutingNumber = routingNumber,
            BankName = bankName,
            Address1 = address1,
            Address2 = doc.DocumentElement.SelectSingleNode("address2")?.InnerText,
            City = city,
            State = state,
            Zip = zip,
            Name = name,
            Description = doc.DocumentElement.SelectSingleNode("description")?.InnerText,
            Status = PayoffAccountStatusEnum.PendingApproval, // TODO: Deterine initial status based on who added payoff account
            CreatedDate = DateTime.Now,
            CreatedByUsername = doc.DocumentElement.SelectSingleNode("createdByUsername")?.InnerText
          };

          var createdByIdText = doc.DocumentElement.SelectSingleNode("createdById")?.InnerText;
          if (string.IsNullOrEmpty(createdByIdText) == false)
          {
            Guid createdById;
            if (Guid.TryParse(createdByIdText, out createdById))
            {
              newPayoffAccount.CreatedBy_Id = createdById;
            }
          }

          var createdByEntityIdText = doc.DocumentElement.SelectSingleNode("createdByEntityId")?.InnerText;
          if (string.IsNullOrEmpty(createdByEntityIdText) == false)
          {
            Guid createdByEntityId;
            if (Guid.TryParse(createdByEntityIdText, out createdByEntityId))
            {
              newPayoffAccount.CreatedByEntity_Id = createdByEntityId;
            }
          }

          db.PayoffBankAccounts.Add(newPayoffAccount);
          db.SaveChanges();

          return new ResultMessage(ResultEnum.Success, "Success.");
        }
      }
      catch (Exception ex)
      {
        // TODO: log the actual exception
        return new ResultMessage(ResultEnum.Error, "There was a problem creating a new payoff account.");
      }
    }
  }
}