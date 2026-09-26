using System;
using System.Collections.Generic;
using OneExtend.Core.Commands;
using OneExtend.Core.Events;

namespace OneExtend.Core.Modules
{
    /// <summary>
    /// A self-contained feature domain (e.g. "CodeBlock"). Modules are the
    /// unit of extensibility: a future, unrelated feature is a new module and
    /// requires no change to the host shell.
    /// </summary>
    public interface IModule
    {
        string Id { get; }
        string Name { get; }
        string Version { get; }
        void Initialize(IModuleContext context);
    }

    public interface IModuleContext
    {
        ICommandRegistry Commands { get; }
        IEventBus Events { get; }
        IServiceProvider Services { get; }
    }

    /// <summary>Initializes enabled modules in deterministic (registration) order.</summary>
    public sealed class ModuleHost
    {
        private readonly List<IModule> _modules = new List<IModule>();

        public void Register(IModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            _modules.Add(module);
        }

        public IReadOnlyList<string> InitializeEnabled(IModuleContext context, ISet<string> disabledModuleIds)
        {
            var initialized = new List<string>();
            foreach (var module in _modules)
            {
                if (disabledModuleIds != null && disabledModuleIds.Contains(module.Id))
                    continue;
                module.Initialize(context);
                initialized.Add(module.Id);
            }
            return initialized;
        }
    }
}
