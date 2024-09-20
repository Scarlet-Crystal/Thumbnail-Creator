# What is this?

This is a utility for creating thumnails for VRChat worlds and avatars. It offers the following advantages over the VRChat SDK's builtin thumbnail capture:
- Can perform Super-Sampled anti-aliasing
- Can render thumbnails while in play mode
- Saves thumbnails to disk, which allows for further editing

# How to install

1. To use this package, you will need the [VRChat Creator Companion](https://vcc.docs.vrchat.com).
2. Go to https://scarlet-crystal.github.io/VCC-Package-Listing/ and click *Add to VCC.*
3. Go to the projects tab, click *Manage Project* for the project you wish to install this package to, then add the *Thumbnail Creator* package to your project.

# How to use

1. Right-click on the hierarchy and select Thumbnail Creator. Alternatively select Toolbar > GameObject > Thumbnail Creator. This will create a new GameObject in the scene call *ThumbnailCreator.*
2. Configure the new GameObject as desired, then click render on the ThumbnailCreator component. This will render the thumbnail and save it to the specified location.