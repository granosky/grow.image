using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.IO;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.InteropServices;

using Rhino.DocObjects;
using Rhino.Collections;
using GH_IO;
using GH_IO.Serialization;

namespace growImage
{
    /// <summary>
    /// This class will be instantiated on demand by the Script component.
    /// </summary>
    public class Script_Instance : GH_ScriptInstance
    {
        #region Utility functions
        /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
        /// <param name="text">String to print.</param>
        private void Print(string text) { /* Implementation hidden. */ }
        /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
        /// <param name="format">String format.</param>
        /// <param name="args">Formatting parameters.</param>
        private void Print(string format, params object[] args) { /* Implementation hidden. */ }
        /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
        /// <param name="obj">Object instance to parse.</param>
        private void Reflect(object obj) { /* Implementation hidden. */ }
        /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
        /// <param name="obj">Object instance to parse.</param>
        private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
        #endregion

        #region Members
        /// <summary>Gets the current Rhino document.</summary>
        private readonly RhinoDoc RhinoDocument;
        /// <summary>Gets the Grasshopper document that owns this script.</summary>
        private readonly GH_Document GrasshopperDocument;
        /// <summary>Gets the Grasshopper script component that owns this script.</summary>
        private readonly IGH_Component Component;
        /// <summary>
        /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
        /// Any subsequent call within the same solution will increment the Iteration count.
        /// </summary>
        private readonly int Iteration;
        #endregion

        /// <summary>
        /// This procedure contains the user code. Input parameters are provided as regular arguments,
        /// Output parameters as ref arguments. You don't have to assign output parameters,
        /// they will have a default value.
        /// </summary>
        private void RunScript(bool reset, bool draw, string image, int width, int height, ref object growDots)
        {
            // Load image.
            Bitmap bitmap = new Bitmap(image);

            // Resize image.
            bitmap = new Bitmap(bitmap, width, height);

            // Define array for output data.
            double[,] brightness = new double[width, height];

            // Harvest brightness data.
            for (int x = 0; x < width; x++)

                for (int y = 0; y < height; y++)
                {
                    Color c = bitmap.GetPixel(x, y);
                    // Mirror the pixel reading order.
                    brightness[x, height - y - 1] = c.GetBrightness();
                }

            // Convert array to list.
            List<double> brightList = ConvertArrayToList(brightness);

            // Remap brigtness values.
            List<double> brightPow = new List<double>();

            for (int i = 0; i < brightList.Count; i++)
            {
                brightPow.Add(Math.Pow(brightList[i], -1));
            }

            // Final list with radius for the dot image.
            List<double> finalRadius = new List<double>();

            for (int i = 0; i < brightPow.Count; i++)
            {
                double mapValue = MapValue(0.0, 2.45, 0.01, 0.45, brightPow[i]);

                if (mapValue > 0.45)
                {
                    mapValue = 0.45;
                }
                finalRadius.Add(mapValue);
            }

            // Target/final pixels with circles.
            List<Point3d> circleCenters = new List<Point3d>();
            List<Circle> finalCircles = new List<Circle>();

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Point3d Points = new Point3d(i, j, 0);
                    circleCenters.Add(Points);
                }
            }

            for (int i = 0; i < circleCenters.Count; i++)
            {
                Circle circle = new Circle(circleCenters[i], finalRadius[i]);
                finalCircles.Add(circle);
            }

            // Reset button to start random pixels distribution (radius of circles).
            if (reset)
            {
                randomNum = new List<double>();
                Random randomGenerator = new Random();

                for (int i = 0; i < circleCenters.Count; i++)
                {
                    double myRandomNumber = randomGenerator.NextDouble() * 0.2 + 0.1;
                    randomNum.Add(myRandomNumber);
                }
                num = randomNum;
                check = new int[width * height];
            }

            // Pixel(circles) pulsing before drawing the final image.
            List<Circle> circles = new List<Circle>();
            List<double> numStop = new List<double>();

            for (int i = 0; i < circleCenters.Count; i++)
            {
                // When the draw button is on, stop pulsing when the pixels are in the correct range.
                if (draw && num[i] >= finalRadius[i] - 0.02 && num[i] <= finalRadius[i] + 0.02) num[i] = finalRadius[i];    //to jest do poprawy, bug z breakIt

                // Pulsing -> Enlarge the circles or make them smaller if they are too big or small.
                else
                {
                    if (num[i] >= 0.5) check[i] = 1;
                    else if (num[i] <= 0.01) check[i] = 0;

                    if (check[i] == 1) num[i] -= 0.01;
                    else num[i] += 0.01;
                }
                Circle circle = new Circle(circleCenters[i], num[i]);
                circles.Add(circle);
            }

            growDots = circles;
        }

        public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
        {
            throw new NotImplementedException();
        }

        // <Custom additional code> 

        // Remember the last data.
        List<double> num = new List<double>();
        List<double> randomNum = new List<double>();
        int[] check = new int[10000];

        private List<double> ConvertArrayToList(double[,] Array)
        {
            int sizeX = Array.GetLength(0);
            int sizeY = Array.GetLength(1);
            List<double> list = new List<double>();

            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    list.Add(Array[x, y]);
                }
            }

            return list;
        }

        private double MapValue(double a0, double a1, double b0, double b1, double a)
        {
            /* a = Value to map
               a0 - a1 = source values
               b0 - b1 = target values
            */
            return b0 + (b1 - b0) * ((a - a0) / (a1 - a0));
        }

        // </Custom additional code> 
    }
}