---
uid: articles.start.dev
title: Making your own mod
---

# Making a mod

## Overview

What follows is a *very* barebones, and frankly not very useful plugin class, even as a starting point,
but it should be enough to give a decent idea of how to do quick upgrades of existing mods for those who want to.

[!code-cs[Plugin.cs](./dev-resources/Plugin.cs?range=1-3,6-8,12-16,29-37,39,44-45,46-49,54-57,59-)]

There are basically 4 major concepts here:

1. <xref:IPA.Logging.Logger>, the logging system.
2. <xref:IPA.PluginAttribute>, which declares that this class is a plugin and how it should behave.
3. <xref:IPA.InitAttribute>, which declares the constructor (and optionally other methods) as being
   used for initialization.
4. The lifecycle event attributes <xref:IPA.OnStartAttribute> and <xref:IPA.OnExitAttribute>.

Read the docs at those links for a better idea of what they do.

TODO: expand this to explain more, and expand on the base example
