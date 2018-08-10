using System;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Xml;
using SESServices.DataTransferObjects;
using SESServices.Enumerations;

namespace SESServices.Controllers
{
  public class PayoffAccountsController : ControllerBase
  {
    /// <summary>
    /// Gets account information for the autocomplete function on the edit disbursement line item dialog
    /// </summary>
    /// <param name="accountNumber">The account number the user has entered</param>
    /// <returns>JSon object containing all the necessary information to fill the edit disbursement line item dialog</returns>
    [Route("GetPayoffAccountByAccountNumber")]
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
        var exactMatch = _entities.PayoffBankAccounts.FirstOrDefault(a => a.IsActive && a.AccountInfo_AccountNumber.Equals(accountNumber.Trim(), StringComparison.InvariantCultureIgnoreCase));
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
        var wildcardAccounts = _entities.PayoffBankAccounts.Where(a => a.IsActive && a.AccountInfo_AccountNumber.Contains("X"));
        foreach (var wildcardAccount in wildcardAccounts)
        {
          // If the passed in account number does not match the length of the wildcard number, move to the next one
          if (accountNumber.Length != wildcardAccount.AccountInfo_AccountNumber.Length) continue;

          for (var i = 0; i < accountNumber.Length; i++)
          {
            var accountChar = accountNumber[i];
            var wildcardChar = wildcardAccount.AccountInfo_AccountNumber[i];

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
        return new PayoffAccountAutocomplete
        {
          Result = ResultEnum.Error,
          Message = "There was a problem retreiving the payoff account information."
        };
      }

    }



    [Route("CreatePayoffAccount")]
    [HttpPost]
    public ResultMessage CreatePayoffAccount(HttpRequestMessage request)
    {
      var doc = new XmlDocument();
      doc.Load(request.Content.ReadAsStreamAsync().Result);

      if (doc.DocumentElement == null)
      {
        return new ResultMessage(ResultEnum.Failure, "Unable to open xml document.");
      }

      var accountNumber = doc.DocumentElement.SelectSingleNode("accountNumber");
      var routingNumber = doc.DocumentElement.SelectSingleNode("routingNumber");

      return new ResultMessage(ResultEnum.Success, "Success.");

    }




  }
}