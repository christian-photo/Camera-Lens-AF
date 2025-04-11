using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin
[assembly: Guid("df0bb056-bd8b-4939-95f6-a175ca252fa5")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("2.2.0.0")]
[assembly: AssemblyFileVersion("2.2.0.0")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Lens AF")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Run AF with your Camera Lens!")]

// Your name
[assembly: AssemblyCompany("Christian Palm")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Camera Lens AF")]
[assembly: AssemblyCopyright("Copyright ©  2025")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.2017")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/rennmaus-coder/Camera-Lens-AF")]


// The following attributes are optional for the official manifest meta data

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "AF,Sequencer,Canon")]

//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"This plugin provides a focuser driver for lenses attachted to Canon and Nikon cameras

To be able to use the driver, a Canon or Nikon camera has to be connected.

## Important Note!
**Test the plugin before you use it in your imaging runs, because this plugin may not work for everyone! This plugin doesn't work with ASCOM.DSLR unfortuantely!**  
If you have questions/feedback/issues, you can ask on the [NINA discord](https://discord.com/invite/nighttime-imaging) in #plugin-discussions or create an issue [here](https://github.com/rennmaus-coder/Camera-Lens-AF/issues)


**Requirements**:  
- A Canon or Nikon Camera,  
- A Camera Lens that supports AF

The plugin was tested using the following camera and lenses (Note that there are often issues with third-party lenses, so be aware of that):
- Canon EOS 600d
- Canon EF 100-400 f/4.5-5.6 L IS USM
- Canon EF 24-105 f/4 L IS USM
- Nikon Z6 with Nikon Nikkor Z 70-200 f/2.8

## Manual Focus Control controls (The Manual Focus Control does not affect the focuser position!)
- Mousewheel/Trackpad: Zoom in/out
- Left mousebutton: Pan around
- Right mousebutton: reset


Known issues:
- Some lenses may try to refocus, when taking images if the lens is set to AF")]


// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
// [Unused]
[assembly: AssemblyConfiguration("")]
// [Unused]
[assembly: AssemblyTrademark("")]
// [Unused]
[assembly: AssemblyCulture("")]