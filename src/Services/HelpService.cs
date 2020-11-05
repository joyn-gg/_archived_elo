using Discord;
using Discord.Commands;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ELO.Extensions;
using ELO.Services.Reactive;

namespace ELO.Services
{
    public partial class HelpService
    {
        public CommandService CommandService { get; }
        public IServiceProvider Provider { get; }

        public HelpService(CommandService cmdService, IServiceProvider provider)
        {
            CommandService = cmdService;
            Provider = provider;
        }

        private string ReplaceLastOccurrence(string Source, string Find, string Replace)
        {
            int place = Source.LastIndexOf(Find);

            if (place == -1)
                return Source;

            string result = Source.Remove(place, Find.Length).Insert(place, Replace);
            return result;
        }

        public virtual async Task<List<Tuple<string, List<CommandInfo>>>> GetFilteredModulesAsync(ShardedCommandContext context = null, bool usePreconditions = true, List<string> moduleFilter = null, string[] preconditionSkips = null)
        {
            var commandCollection = CommandService.Commands.ToList();

            var modules = commandCollection.GroupBy(c => c.Module.Name).Select(x => new Tuple<string, List<CommandInfo>>(x.Key, x.ToList())).ToList();

            //Use filter out any modules that are not chosen in the filter.
            if (moduleFilter != null && moduleFilter.Any())
            {
                modules = modules.Where(x => moduleFilter.Any(f => x.Item1.Contains(f, StringComparison.InvariantCultureIgnoreCase))).ToList();
            }

            if (usePreconditions)
            {
                var newModules = new List<Tuple<string, List<CommandInfo>>>();
                for (int i = 0; i < modules.Count; i++)
                {
                    var module = modules[i];
                    var commands = new List<CommandInfo>();
                    foreach (var command in module.Item2)
                    {
                        if (await CheckPreconditionsAsync(context, command, preconditionSkips))
                        {
                            commands.Add(command);
                        }
                    }

                    if (commands.Any())
                    {
                        newModules.Add(new Tuple<string, List<CommandInfo>>(module.Item1, commands));
                    }
                }

                modules = newModules;
            }

            modules = modules.OrderBy(m => m.Item1).ToList();
            return modules;
        }

        public virtual async Task<ReactivePager> PagedHelpAsync(ShardedCommandContext context, bool usePreconditions = true, List<string> moduleFilter = null, string additionalField = null, string[] preconditionSkips = null)
        {
            var modules = await GetFilteredModulesAsync(context, usePreconditions, moduleFilter, preconditionSkips);

            var overviewFields = new List<EmbedFieldBuilder>
            {
                new EmbedFieldBuilder
                {
                    // This gives a brief overview of how to use the paginated message and help commands.
                    Name = $"Commands Summary",
                    Value =
                    (usePreconditions ? "I have gone and hidden commands which you do not have sufficient permissions to use.\n" : "") +
                    "Go to the respective page number of each module to view the commands in more detail.\n" +
                    additionalField
                }
            };

            foreach (var commandGroup in modules)
            {
                var moduleName = commandGroup.Item1;
                var commands = commandGroup.Item2;

                //This will be added to the 'overview' for each module
                var moduleContent = new StringBuilder();
                moduleContent.AppendJoin(", ", commands.GroupBy(x => x.Name).Select(x => "`" + ReplaceLastOccurrence(x.First().Aliases.First(), x.Key.ToLowerInvariant(), x.Key) + "`").OrderBy(x => x));

                //Handle modules with the same name and group them.
                var duplicateModule = overviewFields.FirstOrDefault(x => x.Name.Equals(moduleName, StringComparison.InvariantCultureIgnoreCase));
                if (duplicateModule != null)
                {
                    if (duplicateModule.Value is string value)
                    {
                        duplicateModule.Value = $"{value}\n{moduleContent.ToString()}".FixLength();
                    }
                }
                else
                {
                    overviewFields.Add(new EmbedFieldBuilder
                    {
                        Name = moduleName,
                        Value = moduleContent.ToString().FixLength()
                    });
                }
            }

            int pageIndex = 0;
            var pages = new List<Tuple<int, ReactivePage>>();
            foreach (var commandGroup in modules)
            {
                var moduleName = commandGroup.Item1;
                var commands = commandGroup.Item2;

                //This will be it's own page in the paginator
                var page = new ReactivePage();
                var pageContent = new StringBuilder();
                var commandContent = new StringBuilder();
                foreach (var command in commands.OrderBy(x => x.Aliases.First()))
                {
                    commandContent.AppendLine($"**{command.Name ?? command.Module.Aliases.FirstOrDefault()}**");
                    if (command.Summary != null)
                    {
                        commandContent.AppendLine($"[Summary]{command.Summary}");
                    }
                    if (command.Remarks != null)
                    {
                        commandContent.AppendLine($"Remarks: {command.Remarks}");
                    }
                    if (command.Preconditions.Any())
                    {

                        commandContent.AppendLine($"Preconditions: \n{GetPreconditionSummaries(command.Preconditions)}");
                    }
                    if (command.Aliases.Count > 1)
                    {
                        commandContent.AppendLine($"Aliases: {string.Join(",", command.Aliases)}");
                    }
                    commandContent.AppendLine($"`{command.Aliases.First() ?? command.Module.Aliases.FirstOrDefault()} {string.Join(" ", command.Parameters.Select(x => x.ParameterInformation()))}`");

                    if (pageContent.Length + commandContent.Length > 2047)
                    {
                        page.Title = moduleName;
                        page.Description = pageContent.ToString();
                        pages.Add(new Tuple<int, ReactivePage>(pageIndex, page));
                        pageIndex++;
                        page = new ReactivePage();
                        pageContent.Clear();
                        pageContent.Append(commandContent.ToString());
                    }
                    else
                    {
                        pageContent.Append(commandContent.ToString());
                    }
                    commandContent.Clear();
                }

                page.Title = moduleName;
                page.Description = pageContent.ToString();
                //TODO: Add module specific preconditions in additional field
                //Otherwise apply those preconditions to all relevant command precondition descriptions

                var modPreconditons = commandGroup.Item2.SelectMany(x => x.Module.Preconditions).DistinctBy(x => x.GetType().ToString());
                if (modPreconditons.Any())
                {
                    page.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = "Module Preconditions",
                        Value = GetPreconditionSummaries(modPreconditons) ?? "N/A"
                    });
                }

                pages.Add(new Tuple<int, ReactivePage>(pageIndex, page));
                pageIndex++;
            }

            //This division will round upwards (overviewFields.Count - 1)/5 +1
            int overviewPageCount = ((overviewFields.Count - 1) / 5) + 1;

            for (int i = 0; i < pages.Count; i++)
            {
                //Use indexing rather than foreach to avoid updating a collection while it is being interated
                pages[i] = new Tuple<int, ReactivePage>(pages[i].Item1 + overviewPageCount + 1 /* Use + 1 as the pages are appended to the overview page count */ , pages[i].Item2);
            }

            var overviewPages = new List<ReactivePage>();
            foreach (var fieldGroup in overviewFields.SplitList(5))
            {
                overviewPages.Add(new ReactivePage
                {
                    Fields = fieldGroup.Select(x =>
                    {
                        //Modify all overview names to include the page index for the complete summary
                        x.Name = $"[{pages.FirstOrDefault(p => p.Item2.Title.Equals(x.Name))?.Item1.ToString() ?? $"1-{overviewPageCount}"}] {x.Name}";
                        return x;
                    }).ToList()
                });
            }

            overviewPages.AddRange(pages.Select(x => x.Item2));
            var pager = new ReactivePager
            {
                Pages = overviewPages,
                Color = Color.Green,
                Title = $"{context.Client.CurrentUser.Username} Commands"
            };
            pager.Pages = overviewPages;

            return pager;
        }

        public virtual string GetPreconditionSummaries(IEnumerable<PreconditionAttribute> preconditions)
        {
            var preconditionString = string.Join("\n", preconditions.Select(x =>
            {
                return x.GetType().Name;
                /*if (x is PreconditionBase preBase)
                {
                    return $"__{preBase.Name()}__ {preBase.PreviewText()}";
                }
                else
                {
                    return x.GetType().Name;
                }*/
            }).Distinct().ToArray());

            return preconditionString;
        }

        public virtual async Task<bool> CheckPreconditionsAsync(ShardedCommandContext context, CommandInfo command, string[] preconditionSkipTypes = null)
        {
            if (context == null)
            {
                return true;
            }
            var preconditions = new List<PreconditionAttribute>();
            preconditions.AddRange(command.Preconditions);
            preconditions.AddRange(command.Module.Preconditions);
            foreach (var precondition in preconditions)
            {
                if (preconditionSkipTypes?.Contains(precondition.GetType().Name, StringComparer.InvariantCultureIgnoreCase) == true)
                {
                    continue;
                }

                var result = await precondition.CheckPermissionsAsync(context, command, Provider);
                if (result.IsSuccess)
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}