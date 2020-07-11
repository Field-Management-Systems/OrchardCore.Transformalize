using OrchardCore.Alias.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using TransformalizeModule.Fields;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.Features.Services;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TransformalizeModule {
   public class Migrations : DataMigration {

      private readonly IContentDefinitionManager _contentDefinitionManager;
      private readonly IModuleService _moduleService;
      private readonly ILogger<Migrations> _logger;

      public Migrations(
         IContentDefinitionManager contentDefinitionManager,
         IModuleService moduleService,
         ILogger<Migrations> logger
      ) {
         _contentDefinitionManager = contentDefinitionManager;
         _moduleService = moduleService;
         _logger = logger;
      }

      public int Create() {

         _contentDefinitionManager.AlterPartDefinition("TransformalizeReportPart", part => part
             .WithDisplayName("Transformalize Report Part")
             .WithDescription("Provides fields for Transformalize Report content type")
             .WithField("Arrangement", field => field
                 .OfType(nameof(TransformalizeArrangementField))
                 .WithDisplayName("Arrangement")
                 .WithPosition("1")
             ).WithField("PageSizes", field => field
                 .OfType(nameof(PageSizesField))
                 .WithDisplayName("Page Sizes")
                 .WithPosition("2")
             ).WithField("BulkActions", field => field
                .OfType(nameof(BooleanField))
                .WithDisplayName("Bulk Actions")
                .WithPosition("3")
                .WithSettings(new BooleanFieldSettings {
                   DefaultValue = false,
                   Hint = "Allow user to select one, many, or all records for a bulk action?",
                   Label = "Bulk Actions"
                  }
                )
              ).WithField("BulkActionValueField", field => field
                .OfType(nameof(TextField))
                .WithDisplayName("Bulk Action Value Field")
                .WithPosition("4")
                .WithSettings(new TextFieldSettings {
                   Required = false,
                   Hint = "Specify which field or calculated field provides the value for bulk actions."
                  }
                )
              )
         );

         _contentDefinitionManager.AlterTypeDefinition("TransformalizeReport", builder => builder
             .Creatable()
             .Listable()
             .WithPart("TitlePart", part => part.WithPosition("1"))
             .WithPart("AliasPart", part => part
                 .WithPosition("2")
                 .WithSettings(new AliasPartSettings {
                    Pattern = "{{ ContentItem | title | slugify }}"
                 })
             )
             .WithPart("TransformalizeReportPart", part => part.WithPosition("3"))
             .WithPart("CommonPart", part => part.WithPosition("4"))
         );

         _contentDefinitionManager.AlterPartDefinition("TransformalizeTaskPart", part => part
             .WithDisplayName("Transformalize Task Part")
             .WithDescription("Provides fields for Transformalize Task content type")
             .WithField("Arrangement", field => field
                 .OfType(nameof(TransformalizeArrangementField))
                 .WithDisplayName("Arrangement")
                 .WithPosition("1")
             )
         );

         _contentDefinitionManager.AlterTypeDefinition("TransformalizeTask", builder => builder
             .Creatable()
             .Listable()
             .WithPart("TitlePart", part => part.WithPosition("1"))
             .WithPart("AliasPart", part => part
                 .WithPosition("2")
                 .WithSettings(new AliasPartSettings {
                    Pattern = "{{ ContentItem | title | slugify }}"
                 })
             )
             .WithPart("TransformalizeTaskPart", part => part.WithPosition("3"))
             .WithPart("CommonPart", part => part.WithPosition("4"))
         );

         return 1;
      }

      private async Task EnableFeature(string id) {
         var availableFeatures = await _moduleService.GetAvailableFeaturesAsync();

         var contentFields = availableFeatures.FirstOrDefault(f => f.Descriptor.Id == id);
         if (contentFields != null) {
            if (!contentFields.IsEnabled) {
               _logger.LogInformation($"Enabling {id}");
               await _moduleService.EnableFeaturesAsync(new[] { id });
            }
         } else {
            _logger.LogError($"Unable to find {id} features required for Transformalize.");
         }
      }
   }
}