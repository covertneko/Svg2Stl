# Svg2Stl
Convert an SVG to a flat STL! I made this pretty much exclusively to speed up my
workflow of creating front panels for various electronics projects (e.g. synth modules).
I have no idea if it will work for anything other than my specific use case.

## Usage
[OpenSCAD](https://www.openscad.org) must be installed and in your `PATH` to convert the generated
scad script to an actual model.

Then, just run `svg2stl input.svg output.stl`.

DPI and curve smoothness can be adjusted via the `--dpi` and `--curvesteps` arguments respectively.

The thickness of the model (in millimeters) can be adjusted with the `--thickness` option.

The SVG units are expected to be either pixels or millimeters.