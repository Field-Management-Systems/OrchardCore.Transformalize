﻿using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;
using System;
using System.Threading.Tasks;


namespace Module.Navigation {
   public class TransformalizeSettingsAdminMenu : INavigationProvider {
      private readonly IStringLocalizer T;

      public TransformalizeSettingsAdminMenu(IStringLocalizer<TransformalizeSettingsAdminMenu> stringLocalizer) {
         T = stringLocalizer;
      }

      public Task BuildNavigationAsync(string name, NavigationBuilder builder) {

         if (!string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase)) {
            return Task.CompletedTask;
         }

         builder.Add(T["Transformalize"], configuration => configuration
            .AddClass("tfl").Id("tfl")
             .Add(T["Settings"], settings => settings
                 .Add(T["Common"], T["Common"], common => common
                     .Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = Common.SettingsGroupId })
                     .Permission(Permissions.ManageTransformalizeSettings)
                     .LocalNav()
                 )));

         return Task.CompletedTask;
      }

   }
}