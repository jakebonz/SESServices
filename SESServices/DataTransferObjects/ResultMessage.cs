using SESServices.Enumerations;

namespace SESServices.DataTransferObjects
{
  public class ResultMessage
  {
    public ResultEnum Result { get; set; }
    public string Message { get; set; }

    public ResultMessage()
    {
    }

    public ResultMessage(ResultEnum result, string message)
    {
      Result = result;
      Message = message;
    }
  }
}