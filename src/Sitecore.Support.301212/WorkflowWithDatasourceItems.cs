
namespace Sitecore.Support.XA.Foundation.Editing.Commands
{
  using Microsoft.Extensions.DependencyInjection;
  using Sitecore;
  using Sitecore.Abstractions;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.DependencyInjection;
  using Sitecore.Diagnostics;
  using Sitecore.ExperienceEditor.Utils;
  using Sitecore.ExperienceEditor.WebEdit.Commands;
  using Sitecore.Layouts;
  using Sitecore.Pipelines.ResolveRenderingDatasource;
  using Sitecore.Workflows.Simple;
  using Sitecore.XA.Foundation.Abstractions;
  using Sitecore.XA.Foundation.SitecoreExtensions.Repositories;
  using System;
  using System.Collections.Generic;
  using Sitecore.Globalization;

  [Serializable]
  public class WorkflowWithDatasourceItems : Sitecore.XA.Foundation.Editing.Commands.WorkflowWithDatasourceItems
  {
    [UsedImplicitly]
    protected new void WorkflowCompleteCallback(WorkflowPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      Context.ClientPage.SendMessage(this, "item:refresh");
      List<Item> list = new List<Item>();
      IEnumerable<Item> itemsFromLayoutDefinedDatasources = ItemUtility.GetItemsFromLayoutDefinedDatasources(args.DataItem, Context.Device, args.DataItem.Language);
      list.AddRange(itemsFromLayoutDefinedDatasources);
      list.AddRange(GetRenderingDataSourceItems(args.DataItem, args.DataItem.Language));
      IEnumerable<Item> personalizationRulesItems = ItemUtility.GetPersonalizationRulesItems(args.DataItem, Context.Device, args.DataItem.Language);
      list.AddRange(personalizationRulesItems);
      IEnumerable<Item> testItems = ItemUtility.GetTestItems(args.DataItem, Context.Device, args.DataItem.Language);
      list.AddRange(testItems);
      foreach (Item item in ItemUtility.FilterSameItems(list))
      {
        if (item.Access.CanWrite() && (!item.Locking.IsLocked() || item.Locking.HasLock()))
        {
          WorkflowUtility.ExecuteWorkflowCommandIfAvailable(item, args.CommandItem, args.CommentFields);
        }
      }
    }

    protected IEnumerable<Item> GetRenderingDataSourceItems(Item item, Language language = null)
    {
      List<Item> list = new List<Item>();
      RenderingReference[] renderings = item.Visualization.GetRenderings(Context.Device, checkLogin: true);
      LazyResetable<BaseCorePipelineManager> requiredResetableService = ServiceLocator.GetRequiredResetableService<BaseCorePipelineManager>();
      IContentRepository service = ServiceLocator.ServiceProvider.GetService<IContentRepository>();
      RenderingReference[] array = renderings;
      for (int i = 0; i < array.Length; i++)
      {
        ResolveRenderingDatasourceArgs resolveRenderingDatasourceArgs = new ResolveRenderingDatasourceArgs(array[i].Settings.DataSource);
        requiredResetableService.Value.Run("resolveRenderingDatasource", resolveRenderingDatasourceArgs);
        if (!string.IsNullOrWhiteSpace(resolveRenderingDatasourceArgs.Datasource))
        {
          bool isStandardPath = LocalTryParse(resolveRenderingDatasourceArgs.Datasource);
          if (isStandardPath)
          {
            ID result = ID.Parse(resolveRenderingDatasourceArgs.Datasource);
            Item item2 = service.GetItem(result);// Patch 521916: cater for language
            if (item2 != null)
            {
              Item item22 = service.GetItem(result, language ?? Language.Current);
              Item itemWithStandardPath = (item22 == null ? item2 : item22);
              list.Add(itemWithStandardPath);
              list.AddRange(itemWithStandardPath.Children);
            }
          }
          else
          {
            Item item3 = service.GetItem(resolveRenderingDatasourceArgs.Datasource);// Patch 521916: cater for language
            if (item3 != null)
            {
              Item item33 = service.GetItem(item3.ID, language ?? Language.Current);
              Item itemWithLocalPage = (item33 == null ? item3 : item33);
              list.Add(itemWithLocalPage);
              list.AddRange(itemWithLocalPage.Children);
            }
          }
        }
      }
      return list;
    }

    public static bool LocalTryParse(string value)
    { 
      ID result = null;
      if (value == null)
      {
        return false;
      }
      if ((value.Length != 38 || value[0] != '{') && !ID.IsID(value))
      {
        return false;
      }
      try
      {
        result = ID.Parse(value);
      }
      catch
      {
        return false;
      }
      return true;
    }
  }
}