using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace IPA.Injector
{
    internal static class PermissionFix
    {
        public static Task FixPermissions(DirectoryInfo root)
        {
            if (!root.Exists) return Task.CompletedTask;

            return Task.Run(() =>
            {
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
                    Logging.Logger.log.Warn("Error configuring permissions in the game install dir");
                    Logging.Logger.log.Warn(e);
                }
            });
        }
    }
}
