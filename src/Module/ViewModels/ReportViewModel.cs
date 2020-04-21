using System;
using System.Collections.Generic;
using System.Linq;
using OrchardCore.ContentManagement;
using Transformalize.Configuration;

namespace Module.ViewModels {
   public class ReportViewModel {

      private Dictionary<string, Parameter> _parameterLookup;
      private Dictionary<string, Parameter> _inlines;
      private Process _process;
      private HashSet<string> _topParameters;

      // temps
      public bool MapPaging { get; set; }
      public bool CalendarPaging { get; set; }
      public bool EnableInlineParameters { get; set; } = true;
      public bool MapEnabled { get; set; }
      public bool CalendarEnabled { get; set; }

      public Process Process {
         get {
            return _process;
         }

         set {
            _process = value;
            _topParameters = null;
            _inlines = null;
         }
      }

      public ContentItem Item { get; set; }

      public ReportViewModel(Process process, ContentItem item) {
         Process = process;
         Item = item;
      }

      public Dictionary<string, Parameter> InlineParameters {
         get {
            if (_inlines != null) {
               return _inlines;
            }
            CalculateWhereParametersGo();
            return _inlines;
         }
      }

      private void CalculateWhereParametersGo() {

         _inlines = new Dictionary<string, Parameter>();
         _topParameters = new HashSet<string>();
         foreach (var parameter in Process.Parameters.Where(p => p.Prompt)) {
            TopParameters.Add(parameter.Name);
         }

         foreach (var field in Process.Entities.First().GetAllFields().Where(f => !f.System && f.Output)) {

            // opt out of inline field consideration
            if (field.Parameter != null && field.Parameter.Equals("None", StringComparison.OrdinalIgnoreCase)) {
               continue;
            }

            if (field.Parameter != null && ParameterLookup.ContainsKey(field.Parameter) && ParameterLookup[field.Parameter].Prompt && !ParameterLookup[field.Parameter].Required) {
               _inlines[field.Alias] = ParameterLookup[field.Parameter];
               _topParameters.Remove(field.Parameter);
            } else if (ParameterLookup.ContainsKey(field.Alias) && ParameterLookup[field.Alias].Prompt && !ParameterLookup[field.Alias].Required) {
               _inlines[field.Alias] = ParameterLookup[field.Alias];
               _topParameters.Remove(field.Alias);
            } else if (ParameterLookup.ContainsKey(field.SortField) && ParameterLookup[field.SortField].Prompt && !ParameterLookup[field.SortField].Required) {
               _inlines[field.Alias] = ParameterLookup[field.SortField];
               _topParameters.Remove(field.SortField);
            }
         }
      }

      public HashSet<string> TopParameters {
         get {
            if (_topParameters != null) {
               return _topParameters;
            }
            CalculateWhereParametersGo();
            return _topParameters;
         }
      }

      public Parameter GetParameterByName(string name) {
         return ParameterLookup[name];
      }

      public Dictionary<string, Parameter> ParameterLookup {
         get {
            if (_parameterLookup != null) {
               return _parameterLookup;
            }

            _parameterLookup = new Dictionary<string, Parameter>();
            foreach (var parameter in Process.Parameters) {
               _parameterLookup[parameter.Name] = parameter;
            }

            return _parameterLookup;
         }
      }

   }
}