using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] The following GUID is used as a unique identifier of the plugin
[assembly: Guid("df0bb056-bd8b-4939-95f6-a175ca252fa5")]

// [MANDATORY] The assembly versioning
//Should be incremented for each new release build of a plugin
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]

// [MANDATORY] The name of your plugin
[assembly: AssemblyTitle("Lens AF")]
// [MANDATORY] A short description of your plugin
[assembly: AssemblyDescription("Run AF with your Camera Lens!")]

// Your name
[assembly: AssemblyCompany("Christian Palm")]
// The product name that this plugin is part of
[assembly: AssemblyProduct("Camera Lens AF")]
[assembly: AssemblyCopyright("Copyright ©  2021")]

// The minimum Version of N.I.N.A. that this plugin is compatible with
[assembly: AssemblyMetadata("MinimumApplicationVersion", "2.0.0.2004")]

// The license your plugin code is using
[assembly: AssemblyMetadata("License", "MPL-2.0")]
// The url to the license
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/rennmaus-coder/Camera-Lens-AF")]


// The following attributes are optional for the official manifest meta data

//[Optional] Common tags that quickly describe your plugin
[assembly: AssemblyMetadata("Tags", "AF,Sequencer,Canon")]

//[Optional] An in-depth description of your plugin
[assembly: AssemblyMetadata("LongDescription", @"This Plugin provides an instruction to run an AF with your camera lens attached to a Canon Camera

**Requirements**:  
- A Canon Camera,  
- A Camera Lens that supports AF
")]


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