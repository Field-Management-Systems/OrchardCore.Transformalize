using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Module.Services.Contracts;
using Module.ViewModels;
using Module.Services;
using Module.Models;

namespace Module.Controllers {
   public class SchemaController : Controller {

      private readonly ISchemaService _schemaService;
      private readonly CombinedLogger<TaskController> _logger;

      public SchemaController(
         ISchemaService taskService,
         CombinedLogger<TaskController> logger
      ) {
         _schemaService = taskService;
         _logger = logger;
      }

      public async Task<ActionResult> Index(string contentItemId, string format = "xml") {

         if (HttpContext == null || HttpContext.User == null || HttpContext.User.Identity == null || !HttpContext.User.Identity.IsAuthenticated) {
            return Unauthorized();
         }

         var user = HttpContext.User?.Identity?.Name ?? "Anonymous";

         var request = new TransformalizeRequest(contentItemId, user) { Format = format };
         var task = await _schemaService.Validate(request);

         if (task.Fails()) {
            return task.ActionResult;
         }

         var process = await _schemaService.GetSchemaAsync(task.Process);

         if (format == null) {
            return View("Log", new LogViewModel(_logger.Log, process, task.ContentItem));
         } else {
            task.Process.Log.AddRange(_logger.Log);
            task.Process.Connections.Clear();
            return new ContentResult() { Content = process.Serialize(), ContentType = request.ContentType };
         }
      }

   }
}