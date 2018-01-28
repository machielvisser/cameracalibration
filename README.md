# EmguCV Experiments
This repository contains several tools and experiments related to using camera's for face tracking purposes using EmguCV

The following frameworks were used in this solution:
* C#
* Emgu
* Reactive
* WPF

## Camera Calibration
EmguCV camera calibration with user interface

This tool lets the user create a so called cameramatrix and distortionmatrix for a camera, and store the results.
Calibration is done using a 10 by 7 chessboard pattern.

With the calibrations, and faces in the image, the angle is calculated between the camera direction and the face on the horizontal plane.

## Face tracking
EmguCV face tracking experiment using the TrackerKCF
