XamGuard adds a new step to the build process where the compiled Java code (from bound libraries) is stripped of unused code by Proguard.


## Why would I need this?

Building applications with Xamarin.Android is a breeze and one can use various components to speed up the development even more. Problem with using a lot of libraries is that the final APK size grows rapidly to something that is hard to justify to the users downloading it. Xamarin team has built an awesome IL linker, which removes unused .NET code from your app.

When using Android support libraries or other Java binding libraries, they not only have large .NET assemblies but large Java class files. Most of the time you will only be using a small subset of those libraries, thus forcing your users to waste room on their phones for that dead code.

The Android SDK bundles Proguard with it and when developing in Java it is very easy to let it strip out the unused Java code (just like Xamarin does for IL). Out of the box, Xamarin.Android doesn't support running Proguard for the Java code, that's where XamGuard steps in.

For example, the JAR file for Google Play Services is around 1.4M. If you only use GCM from it, then after stripping unused code from it, it will only take up 27K.


## Give it to me now!

The easies way to get the binaries is to look for XamGuard in your NuGet package manager.

After getting the binaries, the first thing you need to do is to add a line to your Android .csproj file. This line imports the new build steps to run Proguard. You need to add if _after_ the line importing `Novell.MonoDroid.CSharp.targets`. Like so:

```xml
  <Import Project="$(MSBuildExtensionsPath)\Novell\Novell.MonoDroid.CSharp.targets" />
  <Import Project="..\packages\XamGuard.1.0.0\Proguard.targets" Condition="'$(Configuration)' != 'Debug'" />
```

You should double check that the path to Proguard.targets file is correct. The next step is to configure Proguard.


### Setting up Proguard

The NuGet package adds a [Proguard.cfg](https://github.com/roosmaa/XamGuard/blob/master/NuGet/Proguard.cfg) to your project folder. This is the file where you add project specific Proguard configurations. The template is usually good enough for most cases.

You do need to edit it, whenever you use some Android views from XML files. In that case you need to instruct Proguard explicitly to keep them, like so:

```
-keep class android.support.v4.view.PagerTitleStrip { *; }
-keep class android.support.v4.view.ViewPager { *; }
```

It is possible that in some cases XamGuard fails to detect that some Java function is used by managed code. XamGuard also does not attempt to detect JNIEnv usages in your own code. If that is the case, you should check out [Proguard manual](http://proguard.sourceforge.net/index.html#manual/index.html) on how to best configure Proguard for your usecase.

In short, you **should** always test your release builds before giving them to your users as there might have been something that was removed and will cause your app to crash.


### Building from source

To build XamGuard from source you need to open the solution and build in Release mode. After that you should move the files from `Proguard.Build.Tasks/bin/Release/` to your solution folder where your Android project can import the `Proguard.target`. That's it.


## Contributing

All contributions are more than welcome. Fork the code, add your changes and create a pull request. The usual Github workflow. :)


## License

XamGuard is licensed under [MIT license](https://github.com/roosmaa/XamGuard/blob/master/LICENSE).