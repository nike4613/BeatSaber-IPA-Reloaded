---
uid: articles.start.dev
title: Making your own mod
---

# Making a mod

## Overview

What follows is a *very* barebones, and frankly not very useful plugin class, even as a starting point,
but it should be enough to give a decent idea of how to do quick upgrades of existing mods for those who want to.

[!code-cs[Plugin.cs](./dev-resources/Plugin.cs?range=1-3,6-8,12-16,29-37,39,50-51,52-55,60-63,65-)]

There are basically 4 major concepts here:

1. <xref:IPA.Logging.Logger>, the logging system.
2. <xref:IPA.PluginAttribute>, which declares that this class is a plugin and how it should behave.
3. <xref:IPA.InitAttribute>, which declares the constructor (and optionally other methods) as being
   used for initialization.
4. The lifecycle event attributes <xref:IPA.OnStartAttribute> and <xref:IPA.OnExitAttribute>.

I reccommend you read the docs for each of those to get an idea for what they do.

It is worth noting that this example is of a mod that *cannot* be enabled and disabled at runtime, as marked by
[RuntimeOptions.SingleStartInit](xref:IPA.RuntimeOptions.SingleStartInit).

### What can be changed

Before we go adding more functionality, its worth mentioning that that is not the *only* way to have a plugin set up.

For starters, we can add another *method* marked `[Init]`, and it will be called after the constructor, with the same
injected parameters, if those are applicable.

[!code-cs[Plugin.cs#Init(Logger)](./dev-resources/Plugin.cs?range=40-44)]

If you only had a method marked `[Init]`, and no constructors marked `[Init]`, then the plugin type must expose a
public default constructor. If multiple constructors are marked `[Init]`, only the one with the most parameters will
be called.

You may also mark as many methods as you wish with `[Init]` and all of them will be called, in no well-defined order on
initialization. The same is true for `[OnStart]` and `[OnExit]`, respectively.

## From Scratch

If you are starting from scratch, you will need one other thing to get your plugin up and running: a manifest.

A basic manifest for that might look a little like this:

[!code-json[manifest.json](./dev-resources/manifest.json?range=1,3,4,6-12,14-19,23-)]

There is a lot going on there, but most of it should be decently obvious. Among the things that *aren't* immediately obvious,
are

- `id`: This represents a unique identifier for the mod, for use by package managers such as BeatMods. It may be null if the
  mod chooses not to support those.
- `features`: Don't worry about this for now, this is a not-very-simple thing that will be touched on later.

In addition, there are a few gatchas with it:

- `description`: This can be either a string or an array representing different lines. Markdown formatting is permitted.
- `gameVersion`: This should match *exactly* with the application version of the game being targeted. While this is not enforced
  by BSIPA, mod repositories like BeatMods may require it match, and it is good practice regardless.
- `version`: This must be a valid SemVer version number for your mod.

In order for your plugin to load, the manifest must be embedded into the plugin DLL as an embedded resource. This can be set in
the Visual Studio file properties panel under `Build Action`, or in the `.csproj` like so:

[!code-xml[Demo.csproj#manifest](./dev-resources/Demo.csproj?range=12-13,15)]

At this point, if the main plugin source file and the manifest are in the same source location, and the plugin class is using the
project's default namespace, the plugin will load just fine. However, this is somewhat difficult both to explain and verify, so I
recommend you use the the `misc.plugin-hint` field in your manifest. It can be used like so:

[!code-json[manifest.json#misc.plugin-hint](./dev-resources/manifest.json?range=20-22)]

With this, you can set `plugin-hint` to the full typename of your plugin type, and it will correctly load. This is a hint though,
and will also try it as a namespace if it fails to find the plugin type. If that fails, it will then fall back to using the manifest's
embedded namespace.

### A less painful description

If you want to have a relatively long or well-formatted description for your mod, it may start to become painful to embed it in a list
of JSON strings in the manifest. Luckily, there is a way to handle this.

The first step is to create another embedded file, but this time it should be a Markdown file, perhaps `description.md`. It may contain
something like this:

[!code-markdown[description.md](./dev-resources/description.md)]

Then, in your manifest description, have the first line be something look like this, but replacing `Demo.description.md` with the fully
namespaced name of the resource:

[!code-json[manifest.json#description](./dev-resources/manifest.json?range=5)]

Now, when loaded into memory, if anything reads your description metadata, they get the content of that file instead of the content of the
manifest key.

### Configuring your plugin

Something that many plugins want and need is configuration. Fortunately, BSIPA provides a fairly powerful configuration system out of the
box. To start using it, first create a config class of some kind. Lets take a look at a fairly simple example of this:

[!code-cs[PluginConfig.cs#basic](./dev-resources/PluginConfig.cs?range=9-10,12,15-17,28-30,78-)]

Notice how the class is both marked `public` **and** is not marked `sealed`. For the moment, both of these are necessary. Also notice that
all of the members are properties. While this doesn't change much now, it will be significant in the near future.

Now, how do we get this object off of disk? Simple. Back in your plugin class, change your `[Init]` constructor to look like this:

[!code-cs[Plugin.cs#config-init](./dev-resources/Plugin.cs?range=17-24,26)]

For this to compile, though, we will need to add a few `using`s:

[!code-cs[Plugin.cs#usings](./dev-resources/Plugin.cs?range=4,5)]

With just this, you have your config automatically loading from disk! It's even reloaded when it gets changed mid-game! You can now access
it from anywhere by simply accessing `PluginConfig.Instance`. Make sure you don't accidentally reassign this though, as then you will loose
your only interaction with the user's preferences.

By default, it will be named the same as is in your plugin's manifest's `name` field, and will use the built-in `json` provider. This means
that the file that will be loaded from will be `UserData/Demo Plugin.json` for our demo plugin. You can, however, control both of those by
applying attributes to the <xref:IPA.Config.Config> parameter, namely <xref:IPA.Config.Config.NameAttribute> to control the name, and
<xref:IPA.Config.Config.PreferAttribute> to control the type. If the type preferences aren't registered though, it will just fall back to JSON.

The config's behaviour can be found either later here, or in the remarks section of
<xref:IPA.Config.Stores.GeneratedExtension.Generated``1(IPA.Config.Config,System.Boolean)>.

At this point, your main plugin file should look something like this:

[!code-cs[Plugin.cs](./dev-resources/Plugin.cs?range=1-8,12-16,17-24,26,39,50-51,52-55,60-63,65-)]

***

But what about more complex types than just `int` and `float`? What if you want sub-objects?

Those are supported natively, and so are very easy to set up. We just add this to the config class:

[!code-cs[PluginConfig.cs#sub-basic](./dev-resources/PluginConfig.cs?range=18-19,21,25,31,33)]

Now this object will be automatically read from disk too.

But there is one caveat to this: because `SubThingsObject` is a reference type, *`SubThings` can be null*.

This is often undesireable. The obvious solution may be to simply change it to a `struct`, but that is both not supported *and* potentially
undesirable for other reasons we'll get to later.

Instead, you can use <xref:IPA.Config.Stores.Attributes.NonNullableAttribute>. Change the definition of `SubThings` to this:

[!code-cs[PluginConfig.cs#sub-basic-nonnull](./dev-resources/PluginConfig.cs?range=32-33)]

And add this to the `using`s:

[!code-cs[PluginConfig.cs#includes-attributes](./dev-resources/PluginConfig.cs?range=4)]

This attribute tells the serializer that `null` is an invalid value for the config object. This does, however, require that *you* take extra care
ensure that it never becomes null in code, as that will break the serializer.

***

What about collection types?

Well, you can use those too, but you have to use something new: a converter.

You may be familiar with them if you have used something like the popular Newtonsoft.Json library before. In BSIPA, they lie in the
<xref:IPA.Config.Stores.Converters> namespace. All converters either implement <xref:IPA.Config.Stores.IValueConverter> or derive from
<xref:IPA.Config.Stores.ValueConverter`1>. You will mostly use them with an <xref:IPA.Config.Stores.Attributes.UseConverterAttribute>.

To use them, we'll want to import them:

[!code-cs[PluginConfig.cs#includes-attributes](./dev-resources/PluginConfig.cs?range=1,3,5)]

Then add a field, for example a list field:

[!code-cs[PluginConfig.cs#list-basic](./dev-resources/PluginConfig.cs?range=35-36)]

This uses a converter that is provided with BSIPA for <xref:System.Collections.Generic.List`1>s specifically. It converts the list to
an ordered array, which is then written to disk as a JSON array.

We could also potentially want use something like a <xref:System.Collections.Generic.HashSet`1>. Lets start by looking at the definition
for such a member, then deciphering what exactly it means:

[!code-cs[PluginConfig.cs#set-basic](./dev-resources/PluginConfig.cs?range=38-39)]

The converter we're using here is <xref:IPA.Config.Stores.Converters.CollectionConverter`2>, a base type for converters of all kinds of
collections. In fact, the <xref:IPA.Config.Stores.Converters.ListConverter`1> is derived from this, and uses it for most of its implementation.
If a type implements <xref:System.Collections.Generic.ICollection`1>, <xref:IPA.Config.Stores.Converters.CollectionConverter`2> can convert it.

It, like most other BSIPA provided aggregate converters, provides a type argument overload <xref:IPA.Config.Stores.Converters.CollectionConverter`3>
to compose other converters with it to handle unusual element types.

Now after all that, your plugin class has not changed, and your config class should look something like this:

[!code-cs[PluginConfig.cs#basic-complete](./dev-resources/PluginConfig.cs?range=1,3-6,9-10,12,15-19,21,25-26,28-39,78-)]

***

I mentioned earlier that your config file will be automatically reloaded -- but isn't that a bad thing? Doesn't that mean that the config could change
under your feet without you having a way to tell?

Not so- I just haven't introduced the mechanism.

Define a public or protected virtual method named `OnReload`:

[!code-cs[PluginConfig.cs#on-reload](./dev-resources/PluginConfig.cs?range=61-68)]

This method will be called whenever BSIPA reloads your config from disk. When it is called, the object will already have been populated. Use it to
notify all of your systems that configuration has changed.

***

Now, we know how to read from disk, and how to use unusual types, but how do we write it back to disk?

This config system is based on automatic saving (though we haven't quite gotten to the *automatic* part), and so the config is written to disk whenever
the system recognizes that something has changed. To tell is as much, define a public or protected virtual method named `Changed`:

[!code-cs[PluginConfig.cs#changed](./dev-resources/PluginConfig.cs?range=55-59)]

This method can be called to tell BSIPA that this config object has changed. Later, when we enable automated change tracking, this will also be called
when one of the config's members changes. You can use this body to validate something or, for example, write a timestamp for last change.

***

I just mentioned automated change tracking -- lets add that now.

To do this, just make all of the properties virtual, like so:

[!code-cs[PluginConfig.cs#auto-props](./dev-resources/PluginConfig.cs?range=18-19,24-26,42-53)]

Now, whenever you assign to any of those properties, your `Changed` method will be called, and the config object will be marked as changed and will be
written to disk. Unfortunately, any properties that can be modified while only using the property getter do not trigger this, and so if you change any
collections for example, you will have to manually call `Changed`.

After doing all this, your config class should look something like this:

[!code-cs[PluginConfig.cs#basic-complete](./dev-resources/PluginConfig.cs?range=1,3-6,9-10,12,15-19,24,25-26,42-68,78-)]

***

There is one more major problem with this though: the main class is still public. Most configs shouldn't be. Lets make it internal.

So we make it internal:

[!code-cs[PluginConfig.cs#internal](./dev-resources/PluginConfig.cs?range=14)]

But to make it actually work, we add this outside the namespace declaration:

[!code-cs[PluginConfig.cs#internals-visible](./dev-resources/PluginConfig.cs?range=2,6-7)]

And now our full file looks like this:

[!code-cs[PluginConfig.cs#basic-complete](./dev-resources/PluginConfig.cs?range=1-10,14,15-19,24,25-26,42-68,78-)]
