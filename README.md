# What is this?
This is a utility for creating thumnails for VRChat worlds and avatars. It offers the following advantages over the VRChat SDK's builtin thumbnail capture:
- can perform supersampled antialiasing
- can generate thumbnails while in play mode
# How to use
1. Right-click on the hierarchy and select Thumbnail Creator. Alternatively select Toolbar > GameObject > Thumbnail Creator. This will create a new GameObject in the scene call *ThumbnailCreator.*
2. Configure the new GameObject as desired, then click render on the ThumbnailCreator component. This will render the thumbnail and save it to the specified location.