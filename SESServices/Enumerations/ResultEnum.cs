using System.ComponentModel;

namespace SESServices.Enumerations
{
  public enum ResultEnum
  {
    Unknown = 0,

    [Description("Success")]
    Success,

    [Description("Failure")]
    Failure,

    FailureMissingData,

    FailureImproperlyFormattedData,

    FailureBadRequest,

    FailureDocumentReadError,

    FailureDuplicateData,

    FailureExistingDataNotFound,

    FailureStatusError,

    [Description("Error")]
    Error
  }
}