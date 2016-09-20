using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRL.OctreeLib
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
