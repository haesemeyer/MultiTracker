# MultiTracker
This repository contains the C# code of the tracking program used to track fish in multiwell plates in [Randlett et al., Current Biology, 2018](https://www.cell.com/current-biology/fulltext/S0960-9822(19)30209-X).

Please note that this code is likely not very useful as a whole. It is strongly dependend on the hardware used (EoSens 4CXP Monochrome Camera (Mikroton) running with a Cyton Quad Channel CoaXPress Frame Grabber (Bitflow)). Specifically the code contains a lot of "magic" constants regarding image sizes and frame rates specific to the used setup.

The code depends on the Bitflow SDK for interfacing with the frame grabbber as well as [mhapi](https://github.com/haesemeyer/mhapi) for support functions. Some of the bitflow SDK code has to be integrated directly into the solution for this to compile. Due to copyright reasons this code cannot be included in the repository.