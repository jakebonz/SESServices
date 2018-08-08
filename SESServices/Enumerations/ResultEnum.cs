using System.ComponentModel;

namespace SESServices.Enumerations
{
  public enum ResultEnum
  {
    Unknown = 0,

    [Description("Success")]
    Success = 1,

    [Description("Failure")]
    Failure = 2,

    [Description("Error")]
    Error = 3
  }
}