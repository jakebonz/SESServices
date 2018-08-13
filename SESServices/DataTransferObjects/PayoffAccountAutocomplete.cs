using System.Text;
using SESServices.Enumerations;
using SESServices.Models;

namespace SESServices.DataTransferObjects
{
  public class PayoffAccountAutocomplete
  {
    public ResultEnum Result { get; set; }
    public string Message { get; set; }
    public string Payee { get; set; }
    public string AccountNumber { get; set; }
    public string RoutingNumber { get; set; }
    public string BankName { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }

    public PayoffAccountAutocomplete()
    {
    }

    public PayoffAccountAutocomplete(PayoffBankAccount account)
    {
      Payee = account.Name;
      AccountNumber = account.AccountNumber;
      RoutingNumber = account.RoutingNumber;
      BankName = account.BankName;

      var address1 = new StringBuilder();
      if (!string.IsNullOrWhiteSpace(account.Address1)) address1.Append(Address1);
      if (!string.IsNullOrWhiteSpace(account.Address2)) address1.Append(" " + Address2);
      Address1 = address1.ToString();

      var address2 = new StringBuilder();
      if (!string.IsNullOrWhiteSpace(account.City)) address2.Append(account.City);
      if (!string.IsNullOrWhiteSpace(account.City) && !string.IsNullOrWhiteSpace(account.State)) address2.Append(",");
      if (!string.IsNullOrWhiteSpace(account.State)) address2.Append(" " + account.State);
      if (!string.IsNullOrWhiteSpace(account.Zip)) address2.Append(" " + account.Zip);
    }
  }
}