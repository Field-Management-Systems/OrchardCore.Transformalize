﻿using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;
using Transformalize.Configuration;

namespace Module.Models {

   public class ReportComponents {
      public ContentItem ContentItem { get; set; }
      public TransformalizeReportPart Part { get; set; }
      public Process Process { get; set; }
      public ActionResult ActionResult { get; set; }
      public bool Valid { get; set; }
      public bool Fails() => !Valid;
   }
}
