﻿// Copyright (C) Pash Contributors. License: GPL/BSD. See https://github.com/Pash-Project/Pash/
using System;
using System.Text;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Collections.ObjectModel;
using Pash.Implementation;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace Pash
{
    internal class FullHost
    {
        private bool _interactive;
        private Runspace _currentRunspace;

        public const string BannerText = "Pash - Copyright (C) Pash Contributors. License: GPL/BSD. See https://github.com/Pash-Project/Pash/";

        internal LocalHost LocalHost { get; private set; }

        public FullHost(bool interactive)
        {
            _interactive = interactive;
            LocalHost = new LocalHost(true);
            _currentRunspace = RunspaceFactory.CreateRunspace(LocalHost);
            _currentRunspace.Open();

            LoadProfiles();
        }

        internal void LoadProfiles()
        {
            // currently we only support the current user, current host (semantics of other things in Pash need to
            // be discussed first)
            var curUserCurHost = GetCurrentUserCurrentHostProfilePath();
            LoadProfile(curUserCurHost);

            // finally set the variable with all used profiles
            SetProfileVariable(curUserCurHost, "", "", "");
        }

        internal void LoadProfile(string path)
        {
            if (File.Exists(path))
            {
                Execute(String.Format(". '{0}'", path));
            }
        }

        private string GetCurrentUserCurrentHostProfilePath()
        {
            if (Environment.OSVersion.Platform == System.PlatformID.Win32NT)
            {
                // similar to where the powershell profile would be
                var docDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(docDir, "Pash_profile.ps1");
            }
            else
            {
                // on unix systems it's usually a dotfile in home directory
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                return Path.Combine(homeDir, ".Pash_profile.ps1");
            }
        }

        internal void SetProfileVariable(string curUserCurHost, string curUserAllHosts, string allUsersCurHost, string allUsersAllHosts)
        {
            var profileObj = new PSObject(curUserCurHost);
            var profiles = new Dictionary<string, string> {
                { "CurrentUserCurrentHost", curUserCurHost },
                { "CurrentUserAllHosts", curUserAllHosts },
                { "AllUsersCurrentHost", allUsersCurHost },
                { "AllUsersAllHosts", allUsersAllHosts }
            };
            foreach (var pair in profiles)
            {
                var prop = new PSNoteProperty(pair.Key, pair.Value);
                profileObj.Properties.Add(prop);
                profileObj.Members.Add(prop);
            }
            _currentRunspace.SessionStateProxy.SetVariable("Profile", profileObj);
        }

        void Execute(string cmd)
        {
            var errors = new Collection<object>();
            bool hadSuccess = false;
            try
            {
                // execute the command with no input...
                hadSuccess = executeHelper(cmd, null, ref errors);
            }
            catch (Exception e)
            {
                // An exception occurred that we want to display
                // using the display formatter. To do this we run
                // a second pipeline passing in the error record.
                // The runtime will bind this to the $input variable
                // which is why $input is being piped to out-default
                hadSuccess = false;
                errors.Add(e);
            }
            if (!hadSuccess && errors.Count > 0)
            {
                executeHelper("out-default", new ArrayList(errors).ToArray(), ref errors);
            }
            if (!hadSuccess && !_interactive && LocalHost.ExitCode == 0)
            {
                LocalHost.SetShouldExit(1);
            }
        }


        public int Run()
        {
            return Run(null);
        }

        public int Run(string commands)
        {
            /* LocalHostUserInterface supports getline.cs to provide more comfort for non-Windows users.
             * By default, getline.cs is used on non-Windows systems to hanle user input. However, it can be controlled
             * on all systems with the PASHUseUnixLikeConsole variable. As this has nothing to do with the PS
             * specification, this option is specific to LocalHostUserInterface and cannot be set otherwise.
             */
            var ui = LocalHost.UI;
            var localUI = ui as LocalHostUserInterface;
            if (localUI != null)
            {
                bool? useUnixLikeConsole = GetBoolVariable (PashVariables.UseUnixLikeConsole);
                localUI.UseUnixLikeInput = useUnixLikeConsole ?? localUI.UseUnixLikeInput;
                localUI.InitTabExpansion();
            }

            if (String.IsNullOrEmpty(commands))
            { 
                ui.WriteLine(ConsoleColor.White, ConsoleColor.Black, BannerText);
                ui.WriteLine();
            }
            else
            {
                Execute(commands);
            }

            // If interactive, loop reading commands to execute until ShouldExit is set by
            // the user calling "exit".
            while (!LocalHost.ShouldExit && _interactive)
            {
                Prompt();

                string cmd = ui.ReadLine();

                if (cmd == null)
                {
                    // EOF
                    break;
                }

                Execute(cmd);
            }

            // Exit with the desired exit code that was set by exit command.
            // This is set in the host by the MyHost.SetShouldExit() implementation.
            return LocalHost.ExitCode;
        }

        internal void Prompt()
        {
            Execute("prompt | write-host -nonewline");
        }

        private bool? GetBoolVariable (string name)
        {
            var variable = _currentRunspace.SessionStateProxy.GetVariable(name) as PSVariable;
            if (variable == null)
            {
                return null;
            }

            var value = variable.Value;
            var psObject = value as PSObject;
            if (psObject == null)
            {
                return value as bool?;
            }
            else
            {
                return psObject.BaseObject as bool?;
            }
        }

        private bool executeHelper(string cmd, object[] input, ref Collection<object> errors)
        {
            // Ignore empty command lines.
            if (String.IsNullOrEmpty(cmd))
                return true;

            bool success = true;
            using (var currentPipeline = _currentRunspace.CreatePipeline())
            {
                // A command is not a simple word here, it's the whole user input and might contain
                // multiple commands. Therefore we parse it first, but make sure it's not executed in a local scope
                currentPipeline.Commands.AddScript(cmd, false);

                // Now add the default outputter to the end of the pipe.
                // This will result in the output being written using the PSHost
                // and PSHostUserInterface classes instead of returning objects to the hosting
                // application.
                currentPipeline.Commands.Add("out-default");

                // If there was any input specified, pass it in, otherwise just
                // execute the pipeline.
                ErrorRecord pipelineError = null;
                try
                {
                    if (input != null)
                    {
                        currentPipeline.Invoke(input);
                    }
                    else
                    {
                        currentPipeline.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    // in case of throw statement, parse error, or "ThrowTerminatingError"
                    // append it later to "errors" to preserve the correct order
                    pipelineError = (ex is IContainsErrorRecord) ?
                                    ((IContainsErrorRecord) ex).ErrorRecord : 
                                    new ErrorRecord(ex, "PipelineError", ErrorCategory.InvalidOperation, null);
                }
                // if the pipeline failed, not everything was printed by the out-default command. print the errors
                if (currentPipeline.PipelineStateInfo.State.Equals(PipelineState.Failed))
                {
                    if (errors != null)
                    {
                        errors = currentPipeline.Error.ReadToEnd();
                    }
                    success = false;
                }
                if (pipelineError != null)
                {
                    success = false;
                    var psobj = PSObject.AsPSObject(pipelineError);
                    // the explicit (PS) way of setting internal psobj.WriteToErrorStream = true
                    psobj.Properties.Add(new PSNoteProperty("writeToErrorStream", true));
                    errors.Add(psobj);
                }
            }
            return success;
        }
    }
}
