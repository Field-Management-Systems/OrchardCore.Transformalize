﻿using System.Collections.Generic;

namespace Module.Models {
   public class TransformalizeRequest {
      private string _format = null;

      public string ContentItemId { get; set; } = string.Empty;
      public string User { get; set; }
      public bool Secure { get; set; } = true;
      public string Format {
         get { return _format; }
         set {
            _format = value;
            ContentType = value switch {
               "json" => "application/json",
               "xml" => "application/xml",
               _ => "text/html",
            };
         }
      }
      public string ContentType { get; private set; } = "text/html";
      public Dictionary<string, string> InternalParameters { get; set; } = null;

      public TransformalizeRequest(string contentItemId, string user) {
         User = user ?? "Anonymous";
         ContentItemId = contentItemId;
      }
   }
}
