To include XamGuard into your Xamarin.Android application build process, you need to manually edit your .csproj file. Don't worry, it's adding a single line after the line containing Import element for "Novell.MonoDroid.CSharp.targets".

Like so:

  <Import Project="$(MSBuildExtensionsPath)\Novell\Novell.MonoDroid.CSharp.targets" />
  <Import Project="..\packages\XamGuard.1.0.0\Proguard.targets" Condition="'$(Configuration)' != 'Debug'" />

Make sure to double check that the new line is below the other Import and that the path to your NuGet packages folder is correct.

Now every time you build your app in Release mode the unused Java code will be stripped as well as the IL code. But be sure to test your release builds before uploading them to Google Play as Proguard might have stripped something that your app needs and then you need to manually manage it via the Proguard.cfg file (which was added to your project).
