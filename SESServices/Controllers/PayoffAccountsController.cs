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
          // TODO: Perform entity permission check here

          // TODO: Check to see if the entity passing the payoff account to this can pre-approve payoff accounts

          var doc = new XmlDocument();
          doc.Load(request.Content.ReadAsStreamAsync().Result);

          var validationResult = ValidatePayoffAccountXml(doc);
          if (validationResult.Result != ResultEnum.Success)
          {
            return validationResult;
          }

          var payoffAccount = ConvertXmlToPayoffAccount(doc.DocumentElement);

          var existingPayoffAccount = db.PayoffBankAccounts.FirstOrDefault(p => p.AccountNumber == payoffAccount.AccountNumber && p.RoutingNumber == payoffAccount.RoutingNumber);
          if (existingPayoffAccount != null)
          {
            return new ResultMessage(ResultEnum.FailureDuplicateData, "Unable to create payoff account.  An account with the same account and routing numbers already exists.");
          }

          payoffAccount.Status = PayoffAccountStatusEnum.PendingApproval;  // TODO: Determine beginning status based on entity
          payoffAccount.CreatedDate = DateTime.Now;

          payoffAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
          {
            EntityId = payoffAccount.CreatedByEntity_Id,
            UserProfileId = payoffAccount.CreatedBy_Id,
            TimeStamp = DateTime.Now,
            Username = payoffAccount.CreatedByUsername,
            Message = "Payoff account created."
          });

          db.PayoffBankAccounts.Add(payoffAccount);
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


    [HttpPost]
    public ResultMessage UpdatePayoffAccount(HttpRequestMessage request)
    {
      try
      {
        using (var db = new SES_ServicesEntities())
        {
          // TODO: Perform entity permission check here

          var doc = new XmlDocument();
          doc.Load(request.Content.ReadAsStreamAsync().Result);

          var validationResult = ValidatePayoffAccountXml(doc);
          if (validationResult.Result != ResultEnum.Success)
          {
            return validationResult;
          }

          var updatedAccount = ConvertXmlToPayoffAccount(doc.DocumentElement);

          var duplicateAccount = db.PayoffBankAccounts.FirstOrDefault(p => p.Id != updatedAccount.Id && p.AccountNumber == updatedAccount.AccountNumber && p.RoutingNumber == updatedAccount.RoutingNumber);
          if (duplicateAccount != null)
          {
            return new ResultMessage(ResultEnum.FailureDuplicateData, "Unable to save payoff account information.  The account and routing number pair is already in use by another payoff account.");
          }

          var existingAccount = db.PayoffBankAccounts.FirstOrDefault(p => p.Id == updatedAccount.Id);
          if (existingAccount == null)
          {
            return new ResultMessage(ResultEnum.FailureExistingDataNotFound, "Unable to locate the existing payoff account with the given Id.");
          }

          if (existingAccount.Status != PayoffAccountStatusEnum.PendingApproval)
          {
            return new ResultMessage(ResultEnum.FailureStatusError, "You may only make changes to existing payoff accounts if the account is still pending approval.");
          }

          if (existingAccount.AccountNumber != updatedAccount.AccountNumber)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Account number changed from {existingAccount.AccountNumber} to {updatedAccount.AccountNumber}."
            });
            existingAccount.AccountNumber = updatedAccount.AccountNumber;
          }

          if (existingAccount.RoutingNumber != updatedAccount.RoutingNumber)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Routing number changed from {existingAccount.RoutingNumber} to {updatedAccount.RoutingNumber}."
            });
            existingAccount.RoutingNumber = updatedAccount.RoutingNumber;
          }

          if (existingAccount.Name != updatedAccount.Name)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Name changed from {existingAccount.Name} to {updatedAccount.Name}."
            });
            existingAccount.Name = updatedAccount.Name;
          }

          if (existingAccount.Description != updatedAccount.Description)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Description changed from {(string.IsNullOrEmpty(existingAccount.Description) ? "no description" : existingAccount.Description)} to {(string.IsNullOrEmpty(updatedAccount.Description) ? "no description" : updatedAccount.Description)}."
            });
            existingAccount.Description = updatedAccount.Description;
          }

          if (existingAccount.BankName != updatedAccount.BankName)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Bank Name changed from {existingAccount.BankName} to {updatedAccount.BankName}."
            });
            existingAccount.BankName = updatedAccount.BankName;
          }

          if (existingAccount.Address1 != updatedAccount.Address1)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Address 1 changed from {existingAccount.Address1} to {updatedAccount.Address1}."
            });
            existingAccount.Address1 = updatedAccount.Address1;
          }

          if (existingAccount.Address2 != updatedAccount.Address2)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Address 2 changed from {(string.IsNullOrEmpty(existingAccount.Address2) ? "no address 2" : existingAccount.Address2)} to {(string.IsNullOrEmpty(updatedAccount.Address2) ? "no address 2" : updatedAccount.Address2)}."
            });
            existingAccount.Address2 = updatedAccount.Address2;
          }

          if (existingAccount.City != updatedAccount.City)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"City changed from {existingAccount.City} to {updatedAccount.City}."
            });
            existingAccount.City = updatedAccount.City;
          }

          if (existingAccount.State != updatedAccount.State)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"State changed from {existingAccount.State} to {updatedAccount.State}."
            });
            existingAccount.State = updatedAccount.State;
          }

          if (existingAccount.Zip != updatedAccount.Zip)
          {
            existingAccount.PayoffBankAccountsHistories.Add(new PayoffBankAccountsHistory
            {
              EntityId = updatedAccount.CreatedByEntity_Id,
              UserProfileId = updatedAccount.CreatedBy_Id,
              TimeStamp = DateTime.Now,
              //Username = updatedAccount.CreatedByUsername,
              Message = $"Zip code changed from {existingAccount.Zip} to {updatedAccount.Zip}."
            });
            existingAccount.Zip = updatedAccount.Zip;
          }

          db.SaveChanges();

          return new ResultMessage(ResultEnum.Success, "Payoff account updated.");
        }
      }
      catch (Exception ex)
      {
        return new ResultMessage(ResultEnum.Error, "There was a problem updating the payoff account.");
      }
    }



    private ResultMessage ValidatePayoffAccountXml(XmlDocument doc)
    {
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

      //var state = doc.DocumentElement.SelectSingleNode("state")?.InnerText;
      //if (string.IsNullOrEmpty(state))
      //{
      //  return new ResultMessage(ResultEnum.FailureMissingData, "Unable to create payoff account.  You must provide a state for the bank address.");
      //}

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

      return new ResultMessage(ResultEnum.Success, "Payoff Account add / edit XML Valid");
    }


    private PayoffBankAccount ConvertXmlToPayoffAccount(XmlNode doc)
    {
      var name = doc.SelectSingleNode("name")?.InnerText.Trim();
      var description = doc.SelectSingleNode("description")?.InnerText.Trim();
      var accountNumber = doc.SelectSingleNode("accountNumber")?.InnerText.Trim();
      var routingNumber = doc.SelectSingleNode("routingNumber")?.InnerText.Trim();
      var bankName = doc.SelectSingleNode("bankName")?.InnerText.Trim();
      var address1 = doc.SelectSingleNode("address1")?.InnerText.Trim();
      var address2 = doc.SelectSingleNode("address2")?.InnerText.Trim();
      var city = doc.SelectSingleNode("city")?.InnerText.Trim();
      var state = doc.SelectSingleNode("state")?.InnerText.Trim();
      var zip = doc.SelectSingleNode("zip")?.InnerText.Trim();

      var payoffAccount = new PayoffBankAccount
      {
        Name = name,
        Description = description,
        AccountNumber = accountNumber,
        RoutingNumber = routingNumber,
        BankName = bankName,
        Address1 = address1,
        Address2 = address2,
        City = city,
        State = state,
        Zip = zip
      };

      var idText = doc.SelectSingleNode("id")?.InnerText;
      if (string.IsNullOrEmpty(idText) == false)
      {
        Guid id;
        if (Guid.TryParse(idText, out id))
        {
          payoffAccount.Id = id;
        }
      }

      var createdByIdText = doc.SelectSingleNode("createdById")?.InnerText.Trim();
      if (string.IsNullOrEmpty(createdByIdText) == false)
      {
        Guid createdById;
        if (Guid.TryParse(createdByIdText, out createdById))
        {
          payoffAccount.CreatedBy_Id = createdById;
        }
      }

      var createdByEntityIdText = doc.SelectSingleNode("createdByEntityId")?.InnerText.Trim();
      if (string.IsNullOrEmpty(createdByEntityIdText) == false)
      {
        Guid createdByEntityId;
        if (Guid.TryParse(createdByEntityIdText, out createdByEntityId))
        {
          payoffAccount.CreatedByEntity_Id = createdByEntityId;
        }
      }

      var approvedByIdText = doc.SelectSingleNode("approvedById")?.InnerText.Trim();
      if (string.IsNullOrEmpty(approvedByIdText) == false)
      {
        Guid approvedById;
        if (Guid.TryParse(approvedByIdText, out approvedById))
        {
          payoffAccount.ApprovedBy_Id = approvedById;
        }
      }

      var approvedByEntityIdText = doc.SelectSingleNode("approvedByEntityId")?.InnerText.Trim();
      if (string.IsNullOrEmpty(approvedByEntityIdText) == false)
      {
        Guid approvedByEntityId;
        if (Guid.TryParse(approvedByEntityIdText, out approvedByEntityId))
        {
          payoffAccount.ApprovedByEntity_Id = approvedByEntityId;
        }
      }



      return payoffAccount;
    }
  }
}