using Microsoft.AspNetCore.Mvc;
using Pushthrough.Web.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Pushthrough.Web.Controllers {
    public interface IApnsPassthroughController {
        Task<IActionResult> PushAsync(ApnsPushRequestModel model, CancellationToken ct);
    }
}