using System.Web.Http;
using SESServices.Models;

namespace SESServices.Controllers
{
  public class ControllerBase : ApiController
  {
    protected SES_ServicesEntities _entities;

    protected ControllerBase()
    {
      _entities = new SES_ServicesEntities();
    }
  }
}