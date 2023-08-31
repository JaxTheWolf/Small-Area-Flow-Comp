﻿using System.Numerics;

class Program
{
    static void Main(string[] args)
    {
        // Get gcode file
        string slicerGcodeFilePath = args[0];
        string[] gcodeLines = File.ReadAllLines(slicerGcodeFilePath);

        // Flags that are checked for in slicer gcode
        string[] slicerInfillFlags = { ";TYPE:Solid infill", ";TYPE:Top solid infill" };
        string[] slicerGenericFlags = { ";TYPE:" };

        bool adjustingFlow = false;

        Vector2 previousToolPos = new(0, 0);
        FlowMaths flowMaths = new();

        // Loop through every line of gcode
        for (int index = 0; index < gcodeLines.Count(); index++)
        {
            // Update current tool position
            Vector2 currentToolPos = flowMaths.UpdateToolPos(gcodeLines[index], previousToolPos);


            // Check if it's reading infill gcdoe that needs modified
            if (slicerInfillFlags.Contains(gcodeLines[index]))
                adjustingFlow = true;

            else if (adjustingFlow)
            {
                foreach (string genericFlag in slicerGenericFlags)
                {
                    if (gcodeLines[index].Contains(genericFlag))
                        adjustingFlow = false;
                }
            }


            // If it's set to adjusting the flow
            if (adjustingFlow)
            {
                double oldFlowVal = -1f;
                double newFlowVal = -1f;

                string[] gcodeLineSegments = gcodeLines[index].Split(' ');

                // Loop through each segment of gcode
                for (int i = 0; i < gcodeLineSegments.Count(); i++)
                {
                    // Check if the segment begins with E (extrusion gcode)
                    if (gcodeLineSegments[i][0] == 'E')
                    {
                        oldFlowVal = Convert.ToDouble(gcodeLineSegments[i].Substring(1));

                        // Check if E value isn't a retraction
                        if (oldFlowVal > 0f)
                        {
                            newFlowVal = flowMaths.ModifyFlow(flowMaths.CalcExtrusionLength(currentToolPos, previousToolPos), oldFlowVal);
                            gcodeLineSegments[i] = "E" + newFlowVal.ToString("N5");
                        }
                    }
                }

                // Modify E value if it's been changed (doesn't modify retraction stuff)
                if (oldFlowVal > 0 && oldFlowVal != newFlowVal)
                    gcodeLines[index] = String.Join(' ', gcodeLineSegments) + "; Old Flow Value: " + oldFlowVal + " tool at: X" + currentToolPos.X + " Y" + currentToolPos.Y + " was at: X" + previousToolPos.X + " Y" + previousToolPos.Y;
            }

            // Update previous tool position
            previousToolPos = currentToolPos;
        }

        // Rewrite all the gcode back to the slicer
        File.WriteAllLines(slicerGcodeFilePath, gcodeLines);
        Console.WriteLine("File Parsed");
    }
}


// Class to handle all the flow maths etc.
class FlowMaths
{
    // Smaller value increases the amount of flow reduction
    const int flowModifier = 4;

    private double flowCompModel(double extrusionLength)
    {
        if(extrusionLength == 0)
            return 1;
        return 1 / -(flowModifier * extrusionLength + 1) + 1;
    }

    // Applies flow compensation model
    public double ModifyFlow(double extrusionLength, double eValue)
    {
        return Math.Round(eValue * flowCompModel(extrusionLength), 5);
    }

    // Returns double value of distance between two Vector2's (endPos, startPos)
    public double CalcExtrusionLength(Vector2 endPos, Vector2 startPos)
    {
        return Vector2.Distance(endPos, startPos);
    }

    // Create a 2D Vector for toolhead XY position from GCode Line
    // If gcodeLine doesn't contain an X or Y coord, it's flagged as -999
    private Vector2 vectorPos(string gcodeLine)
    {
        float xPos = -999, yPos = -999;

        if (gcodeLine.Length != 0)
        {
            if (gcodeLine[0] != ';')
            {
                string[] gcodeSegments = gcodeLine.Split(' ');

                foreach (string segment in gcodeSegments)
                {
                    if (segment[0] == 'X' || segment[0] == 'x')
                        xPos = (float)Convert.ToDouble(segment.Substring(1));

                    else if (segment[0] == 'Y' || segment[0] == 'y')
                        yPos = (float)Convert.ToDouble(segment.Substring(1));
                }
            }
        }

        return new(xPos, yPos);
    }

    // Creates a 2D Vector for toolhead's new XY position from a GCode Line
    public Vector2 UpdateToolPos(string gcodeLine, Vector2 previousToolPos)
    {
        Vector2 toolPos = vectorPos(gcodeLine);

        if (toolPos.X == -999)
            toolPos.X = previousToolPos.X;

        if (toolPos.Y == -999)
            toolPos.Y = previousToolPos.Y;

        return toolPos;
    }
}