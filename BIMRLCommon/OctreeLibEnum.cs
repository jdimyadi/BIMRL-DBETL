//
// BIMRL (BIM Rule Language) Simplified Schema ETL (Extract, Transform, Load) library: this library transforms IFC data into BIMRL Simplified Schema for RDBMS. 
// This work is part of the original author's Ph.D. thesis work on the automated rule checking in Georgia Institute of Technology
// Copyright (C) 2013 Wawan Solihin (borobudurws@hotmail.com)
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRL.Common
{
    public enum LineSegmentOverlapEnum : short
    {
        NotOverlap = 1,
        PartiallyOverlap = 2, 
        ExactlyOverlap = 3,
        Touch = 4,
        SubSegment = 5,
        SuperSegment = 6,
        Undefined = 99
    }

    public enum LineSegmentOnSegmentEnum : short
    {
        NotOnTheLine = 1,
        OutsideSegment = 2,
        InsideSegment = 3,
        CoincideEndSegment = 4,
        Undefined = 99
    }

    public enum LineSegmentIntersectEnum : short
    {
        NotIntersected = 1,
        IntersectedWithinSegments = 2,
        IntersectedOutsideSegments = 3,
        Undefined = 99
    }

    public enum FaceIntersectEnum : short
    {
        NotIntersected = 1,
        OverlapFullyContained = 2,
        OverlapFullyContains = 3,
        IntersectPartial = 4,
        IntersectOutside = 5,
        Overlap = 6,
        NoIntersectionParallel = 7,
        Undefined = 99
    }
    public enum PolyhedronFaceTypeEnum : short
    {
        TriangleFaces = 3,
        RectangularFaces = 4,
        ArbitraryFaces = 0
    };
    public enum PolyhedronIntersectEnum : short
    {
        Inside = 1,
        FullyContains = 2,
        Disjoint = 3,
        Intersect = 4,
        Overlap = 5,
        IntersectOrInside = 6
    }

}
