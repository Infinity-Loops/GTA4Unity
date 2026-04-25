/**********************************************************************\

 RageLib
 Copyright (C) 2009  Arushan/Aru <oneforaru at gmail.com>

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.

\**********************************************************************/

using System.IO;
using UnityEngine;

namespace RageLib.Common.ResourceTypes
{
    /// <summary>
    /// 4x3 Transform matrix used for fragment child transforms
    /// RAGE uses column-major layout for matrices
    /// Each column: [X, Y, Z] with column 4 being translation
    /// </summary>
    public struct Matrix34 : IFileAccess
    {
        private float[] M;

        public static Matrix34 Identity
        {
            get
            {
                var m = new Matrix34
                {
                    M = new[]
                    {
                        // Column-major layout (each group of 3 is a column)
                        1f, 0f, 0f,  // Column 0: Right vector
                        0f, 1f, 0f,  // Column 1: Up vector
                        0f, 0f, 1f,  // Column 2: Forward vector  
                        0f, 0f, 0f,  // Column 3: Translation
                    }
                };
                return m;
            }
        }

        // Access by column and row (column-major)
        public float this[int row, int col]
        {
            get { return M[col * 3 + row]; }
            set { M[col * 3 + row] = value; }
        }

        // Linear access
        public float this[int m]
        {
            get { return M[m]; }
            set { M[m] = value; }
        }

        // Extract position from the matrix (column 3)
        // In column-major layout, translation is in the 4th column
        public Vector3 Position
        {
            get { return new Vector3(M[9], M[10], M[11]); }
            set
            {
                M[9] = value.X;
                M[10] = value.Y;
                M[11] = value.Z;
            }
        }

        // Extract rotation matrix (3x3 part)
        public Matrix4x4 GetRotationMatrix()
        {
            var mat = Matrix4x4.identity;
            // Column-major: first 3 columns are rotation
            mat[0, 0] = M[0]; mat[1, 0] = M[1]; mat[2, 0] = M[2];  // Column 0
            mat[0, 1] = M[3]; mat[1, 1] = M[4]; mat[2, 1] = M[5];  // Column 1
            mat[0, 2] = M[6]; mat[1, 2] = M[7]; mat[2, 2] = M[8];  // Column 2
            return mat;
        }

        // Convert to Unity Matrix4x4 (Unity uses column-major too)
        public Matrix4x4 ToMatrix4x4()
        {
            var mat = Matrix4x4.identity;
            
            // Copy rotation columns (first 3 columns)
            mat[0, 0] = M[0]; mat[1, 0] = M[1]; mat[2, 0] = M[2];  // Column 0: Right
            mat[0, 1] = M[3]; mat[1, 1] = M[4]; mat[2, 1] = M[5];  // Column 1: Up
            mat[0, 2] = M[6]; mat[1, 2] = M[7]; mat[2, 2] = M[8];  // Column 2: Forward
            
            // Copy translation (4th column)
            mat[0, 3] = M[9];  mat[1, 3] = M[10]; mat[2, 3] = M[11]; // Column 3: Position
            
            // Set the last row to [0, 0, 0, 1] for homogeneous coordinates
            mat[3, 0] = 0f; mat[3, 1] = 0f; mat[3, 2] = 0f; mat[3, 3] = 1f;
            
            return mat;
        }

        public Matrix34(BinaryReader br) : this()
        {
            Read(br);
        }

        public void Read(BinaryReader br)
        {
            M = new float[12];
            for (int i = 0; i < 12; i++)
            {
                M[i] = br.ReadSingle();
            }
        }

        public void Write(BinaryWriter bw)
        {
            for (int i = 0; i < 12; i++)
            {
                bw.Write(M[i]);
            }
        }
    }
}