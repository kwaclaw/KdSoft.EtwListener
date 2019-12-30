using System;
using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace EtwEvents.WebClient
{
    public class ErrorController: Controller {
        [Route("/error-local-development")]
        public IActionResult ErrorLocalDevelopment([FromServices] IWebHostEnvironment webHostEnvironment) {
            if (!"Development".Equals(webHostEnvironment?.EnvironmentName, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(Resource.Err_NonDevEnvironment);
            }

            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var ex = feature?.Error;

            var problemDetails = new ProblemDetails {
                Status = (int)HttpStatusCode.InternalServerError,
                Instance = feature?.Path,
                Title = ex?.Message ?? "Unexpected Error",
                Detail = ex?.StackTrace,
            };

            return StatusCode(problemDetails.Status.Value, problemDetails);
        }

        [Route("/error")]
        public ActionResult Error([FromServices] IWebHostEnvironment webHostEnvironment) {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var ex = feature?.Error;

            var problemDetails = new ProblemDetails {
                Status = (int)HttpStatusCode.InternalServerError,
                Instance = feature?.Path,
                Title = "Unexpected Error",
                Detail = null,
            };

            return StatusCode(problemDetails.Status.Value, problemDetails);
        }

        // this will be needed if/when we add an error page
        //[HttpPost("/error")]
        //public IActionResult Error([FromBody]ProblemDetails model) {
        //    return View(model ?? new ProblemDetails());
        //}
    }
}