using IPA.Logging;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
#if NET3
using Net3_Proxy;
#endif

namespace IPA.Injector
{
    internal static class PermissionFix
    {
        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler",
            Justification = "I very explicitly want the default scheduler")]
        public static Task FixPermissions(DirectoryInfo root)
        {
            if (!root.Exists) return new Task(() => { });

            return Task.Factory.StartNew(() =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var acl = root.GetAccessControl();

                    var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier));

                    var requestedRights = FileSystemRights.Modify;
                    var requestedInheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                    var requestedPropagation = PropagationFlags.InheritOnly;

                    bool hasRule = false;
                    for (var i = 0; i < rules.Count; i++)
                    {
                        var rule = rules[i];

                        if (rule is FileSystemAccessRule fsrule
                            && fsrule.AccessControlType == AccessControlType.Allow
                            && fsrule.InheritanceFlags.HasFlag(requestedInheritance)
                            && fsrule.PropagationFlags == requestedPropagation
                            && fsrule.FileSystemRights.HasFlag(requestedRights))
                        { hasRule = true; break; }
                    }

                    if (!hasRule)
                    { // this is *sooo* fucking slow on first run
                        acl.AddAccessRule(
                            new FileSystemAccessRule(
                                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                                requestedRights,
                                requestedInheritance,
                                requestedPropagation,
                                AccessControlType.Allow
                            )
                        );
                        root.SetAccessControl(acl);
                    }
                }
                catch (Exception e)
                {
                    Logger.Default.Warn("Error configuring permissions in the game install dir");
                    Logger.Default.Warn(e);
                }
                sw.Stop();
                Logger.Default.Info($"Configuring permissions took {sw.Elapsed}");
            });
        }
    }
}
