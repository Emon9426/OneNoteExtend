using System;
using System.Runtime.InteropServices;
using OneExtend.Addin.Commands;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.Theme;
using OneExtend.Core.Commands;
using OneExtend.Core.Events;
using OneExtend.Core.Modules;
using OneExtend.Infrastructure.Logging;
using OneExtend.Infrastructure.Settings;

namespace OneExtend.Addin
{
    /// <summary>
    /// OneNote desktop COM add-in entry point (ProgId: OneExtend.Connect).
    /// Boots the module host, registers commands and grabs the global hotkeys.
    /// All of OneNote's COM surface stays behind OneExtend.OneNote.
    /// </summary>
    [Guid("8F3A2C5D-6B7E-4A9F-AC01-2D4E5F6A7B8C")]
    [ProgId("OneExtend.Connect")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class Connect : IDTExtensibility2
    {
        private static bool _booted;

        private ILogger _logger = NullLogger.Instance;
        private HotkeyService _hotkeys;
        private ServiceRegistry _services;

        private const int HotkeyInsert = 1; // Ctrl+Alt+C
        private const int HotkeyEdit = 2;   // Ctrl+Alt+E
        private const int HotkeyCopy = 3;   // Ctrl+Shift+C

        public void OnConnection(
            object application,
            ext_ConnectMode connectMode,
            object addInInst,
            ref Array custom)
        {
            try
            {
                Boot();
            }
            catch (Exception ex)
            {
                try { _logger.Error("OnConnection failed", ex); } catch { /* logging itself broken */ }
            }
        }

        private void Boot()
        {
            if (_booted)
                return;
            _booted = true;

            _logger = new FileLogger(FileLogger.DefaultDirectory);
            _logger.Info("OneExtend booting...");

            var settingsStore = new SettingsStore(SettingsStore.DefaultPath);
            var settings = settingsStore.Load();

            var languages = LanguageRegistry.LoadDefault();
            var themes = ThemeLoader.LoadDefault();
            _logger.Info($"Loaded {languages.All.Count} languages, {themes.All.Count} themes.");

            var commands = new CommandRegistry();
            var events = new EventBus();

            _services = new ServiceRegistry();
            _services.Register(_logger);
            _services.Register(settingsStore);
            _services.Register(settings);
            _services.Register(languages);
            _services.Register(themes);
            _services.Register(events);

            var context = new CommandContext(_services);

            commands.Register(new InsertCodeBlockCommand(_services));
            commands.Register(new EditCodeBlockCommand(_services));
            commands.Register(new CopyCodeBlockCommand(_services));

            var moduleHost = new ModuleHost();
            var disabled = new System.Collections.Generic.HashSet<string>();
            if (settings.Modules != null)
                foreach (var kv in settings.Modules)
                    if (kv.Value == false)
                        disabled.Add(kv.Key);
            // The CodeBlock feature ships in-box; future modules register here.
            moduleHost.Register(new CodeBlockModule());
            var initialized = moduleHost.InitializeEnabled(new ModuleContext(commands, events, _services), disabled);
            _logger.Info($"Modules initialized: {string.Join(", ", initialized)}");

            _hotkeys = new HotkeyService(id => DispatchHotkey(id, commands, context));

            if (!_hotkeys.Register(HotkeyInsert, HotkeyService.ModControl | HotkeyService.ModAlt, 0x43 /*C*/))
                _logger.Warn("Failed to register Ctrl+Alt+C (already in use?)");
            if (!_hotkeys.Register(HotkeyEdit, HotkeyService.ModControl | HotkeyService.ModAlt, 0x45 /*E*/))
                _logger.Warn("Failed to register Ctrl+Alt+E (already in use?)");
            if (!_hotkeys.Register(HotkeyCopy, HotkeyService.ModControl | HotkeyService.ModShift, 0x43 /*C*/))
                _logger.Warn("Failed to register Ctrl+Shift+C (already in use?)");

            _logger.Info("OneExtend ready.");
        }

        private void DispatchHotkey(int id, CommandRegistry commands, ICommandContext context)
        {
            try
            {
                ICommand cmd = null;
                switch (id)
                {
                    case HotkeyInsert: commands.TryGet("codeblock.insert", out cmd); break;
                    case HotkeyEdit: commands.TryGet("codeblock.edit", out cmd); break;
                    case HotkeyCopy: commands.TryGet("codeblock.copySource", out cmd); break;
                }
                cmd?.Execute(context);
            }
            catch (Exception ex)
            {
                _logger.Error($"Hotkey {id} failed", ex);
            }
        }

        public void OnDisconnection(ext_DisconnectMode removeMode, ref Array custom)
        {
            try
            {
                _hotkeys?.Dispose();
                _hotkeys = null;
                _logger?.Info("OneExtend disconnected.");
            }
            catch (Exception ex)
            {
                try { _logger?.Error("OnDisconnection failed", ex); } catch { }
            }
        }

        public void OnAddInsUpdate(ref Array custom) { }
        public void OnStartupComplete(ref Array custom) { }
        public void OnBeginShutdown(ref Array custom) { }
    }

    public sealed class CommandContext : Core.Commands.ICommandContext
    {
        public CommandContext(IServiceProvider services) { Services = services; }
        public IServiceProvider Services { get; }
    }

    public sealed class ModuleContext : Core.Modules.IModuleContext
    {
        public ModuleContext(Core.Commands.ICommandRegistry commands, IEventBus events, IServiceProvider services)
        {
            Commands = commands;
            Events = events;
            Services = services;
        }
        public Core.Commands.ICommandRegistry Commands { get; }
        public IEventBus Events { get; }
        public IServiceProvider Services { get; }
    }

    /// <summary>In-box module describing the code block feature domain.</summary>
    public sealed class CodeBlockModule : IModule
    {
        public string Id => "codeblock";
        public string Name => "代码块 Code Block";
        public string Version => "0.1.0";
        public void Initialize(IModuleContext context)
        {
            // Commands are registered by Connect; a module hook point for future features.
        }
    }
}
