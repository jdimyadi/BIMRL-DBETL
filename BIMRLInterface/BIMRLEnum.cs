using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIMRLInterface
{
    public class BIMRLEnum
    {
        public enum IDMemberType
        {
            Id,
            Property,
            Function,
            CurrentObject,
            CurrentValue,
            Construct,
            Undefined
        }

        /// <summary>
        /// Enum for builtin functions that requires only SQL injection. List needs to be updated when BIMRLKeywordMapping is updated
        /// All function should support single quoted expression as a parameter representing something like WHERE clause, e.g. 'Name like \'ABC%\' AND ID < 10'
        /// </summary>
        public enum BuiltinFunction
        {
            AGGREGATEOF,            // returning the aggragate member(s) of an aggregate, usage: AGGREGATEOF(E) where E = aggregate master element
            // returns T/F if AGGREGATEOF(E, M)
            AGGREGATEMASTER,        // returning  the aggregate master element, usage: AGGREGATEMASTER(E)
            BOUNDEDSPACE,           // returning the Space object it bounds, usage: BOUNDEDSPACE(E)
            CLASSIFICATIONOF,       // returning classification of an object, usage: CLASSIFICATIONOF(E)
            CONNECTEDTO,            // returning element that it directly is connected, usage: CONNECTEDTO(E1, E2), CONENCTEDTO(E, <elementtype>)
            CONTAINER,              // returning the container of the element (can be at any level), usage: CONTAINER(E), CONTAINER(E, <spatial eltype>)
            CONTAINS,               // returning T/F whether a container contains the element, usage: CONTAINS(S, E) , parameter qualifier "USEGEOMETRY" applied
            DEPENDENCY,             // returning T/F whether the objects are dependent, usage: DEPENDENCY(E, D)
            DEPENDENTTO,            // returning reference to the host element e.g. DEPENDENTTO(D), or returns T/F if DEPENDENTTO(D, W)
            ELEMENTTYPEOF,          // returning the elementtype of an element, usage: ELEMENTTYPEOF(E)
            GROUPOF,                // returning the group the element belonged to, usage: GROUPOF(E), returns T/F GROUPOF(G, E) 
            HASPROPERTY,            // returning T/F if the element has a specific property, usage: HASPROPERTY(E, <propertyname>) or HASPROPERTY(E, <propertyname>, <psetname>), parameter qualifier (INSTANCEONLY, TYPEONLY, INSTANCEORTYPE) applied
            HASCLASSIFICATION,      // returning T/F if the element has the specified classification, usage: HASCLASSIFICATION(E, <classification code>), HASCLASSIFICATION(E, <classification code>, <classification name>), parameter qualifier (INSTANCEONLY, TYPEONLY, INSTANCEORTYPE) applied
            MATERIALOF,             // returning the material of an element, usage: MATERIALOF(E), parameter qualifier (INSTANCEONLY, TYPEONLY) applied
            MODELINFO,              // returning information about the model, usage: MODELINFO(E)
            OWNERHISTORY,           // returning information about the owner history, usage: OWNERHISTORY(E)
            PROPERTY,               // returning information about the property value, usage: PROPERTY(E, <propertyname>), PROPERTY(E, <propertyname>, <psetname>), parameter qualifier (INSTANCEONLY, TYPEONLY, INSTANCEORTYPE) applied
            PROPERTYOF,             // returning reference to the property object, usage: PROPERTY(E), parameter qualifier (INSTANCEONLY, TYPEONLY, INSTANCEORTYPE) applied
            SPACEBOUNDARY,          // returning space boundary elements, usage: SPACEBOUNDARY(E), SPACEBOUNDARY(S, E) returns T/F
            SYSTEMOF,               // returning System element, usage: SYSTEMOF(E), SYSTEMOF(E, Y) returns T/F
            TYPEOF,                 // returning Type object, usage: TYPEOF(E), TYPEOF(E, T) returns T/F
            ZONEOF,                 // returning Zone object, usage: ZONEOF(E), ZONEOF(E, Z) returns T/F
            UNIQUEVALUE,            // returning list of properties that have count=1 for UNIQUEVALUE()=.T., or count>1 for UNIQUEVALUE()=.F., usage: UNIQUEVALUE(E, <propertname>), UNIQUEVALUE(E, <propertyname>, <psetname>)
            BOUNDARYINFO,           // A function to return information on space boundary details. Qualifiers: COMMONPOINT1, COMMONPOINT2, FACEID1, FACEID2
            DOORLEAF,               // A function to get the relevant face representing the door leaf and returns the face id to it (elementid + id)
            TOP,                    // The set of oriented faces that will return the reference to the associated top face. TOP face
            BOTTOM,                 // BOTTOM face
            SIDE,                   // SIDE face
            UNDERSIDE,              // UNDERSIDE face
            TOPSIDE,                // TOPSIDE face
            UNDEFINED
        }

        /// <summary>
        /// Enum that specifies the type of function specified within BIMRL
        /// </summary>
        public enum FunctionType
        {
            INLINE,                 // Inline function that is valid within the final SQL statement (i.e. builtin underlying SQL statement), no interpretation done
            BUILTIN,                // Type of function that is defined within BIMRL that generally will generate SQL injection
            EXTENSION,              // BIMRL extended function that will be invoked to complement (but separate from) the basic query. It is used primarily in EVALUATE clause
            EXTERNAL,               // External function that is loaded by assembly in BIMRL. This allows support for extension into the language
            UNDEFINED
        }

        public enum Index
        {
            USECURRENT,             // Use the current index value
            NEW                     // Create a new index and advance to it
        }

        public enum functionQualifier
        {
            INSTANCEONLY,           // Evaluate respective information pertaining to Instance only
            TYPEONLY,               // Evaluate respective information pertaining to Type only
            INSTANCEORTYPE,         // Evaluate respective information both from Instance and Type
            USEGEOMETRY,            // Evaluate using Geometry for spatial operation
            PHYSICALBOUNDARY,       // Specific for Space boundary: Physical
            VIRTUALBOUNDARY,        // Specific for Space boundary: Virtual
            EXTERNAL,               // Is external
            INTERNAL,               // Is internal
            COMMONPOINT1,           // a common point of boundary face between 2 elements
            COMMONPOINT2,           // a common point of boundary face between 2 elements
            FACEID1,                // a face id to the first element in the argument list from boundary face
            FACEID2,                // a face id to the second element in the argument list from boundary face
            TOP,                    // Face orientation: TOP
            BOTTOM,                 // Face orientation: BOTTOM
            SIDE,                   // Face orientation: SIDE
            TOPSIDE,                // Face orientation: TOPSIDE
            UNDERSIDE,              // Face orientation: UNDERSIDE
            EXACT,                  // To tell function to operate on exact geometry and not only its approximated shape using the spatial index
            AGGREGATE,              // To tell the function to consolidate the result table into a unique aggregate columns as specified
            TO_NUMBER,              // Change the value to number format
            TO_CHAR,                // Change the value to string
            USE_OBB,                // Option to use Projected OBB, without this option, the default will be AABB
            UNDEFINED
        }

        public enum deferredExecutionMode
        {
            PARALLEL,               // Run multiple deferred executions in parallel, i.e. each receives the same inputTable to process. Results will be merged
            SEQUENCE,               // Run multiple deferred executions in sequence, i.e. one after another is complete. The result from one is passed on to be the inputTable for the next. The last function will generate the final result
            UNDEFINED
        }

    }
}
