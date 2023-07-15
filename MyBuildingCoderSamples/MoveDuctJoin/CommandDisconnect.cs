using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using MyBuildingCoderSamples.HelpingClasses;

namespace MyBuildingCoderSamples.MoveDuctJoin
{
  [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CommandDisconnect : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApplication = commandData.Application;
            UIDocument uiDocument = uiApplication.ActiveUIDocument;
            Document document = uiDocument.Document;
            
            //Declare the variables
            Selection selectedItem = uiDocument.Selection;
            Duct duct = null;
            XYZ pointFrom = null;
            XYZ pointTo = null;

            // try to select the duct object
            try
            {
                Reference reference = selectedItem.PickObject(ObjectType.Element,
                    new SelectionFilters.DuctSelectionFilter(), "Please pick a duct at the " + "connection to move.");
                duct = document.GetElement(reference.ElementId) as Duct;
                pointFrom = reference.GlobalPoint;

                reference = selectedItem.PickObject(ObjectType.Element, new SelectionFilters.DuctSelectionFilter(),
                    "Please pick a target point on the " + "duct to move the connection to.");
                pointTo = reference.GlobalPoint;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Failed;
            }
            
            // Determine connector closet to picked point
            ConnectorSet connectorSet = duct.ConnectorManager.Connectors;
            Connector connector = null; //declare the connector
            double distance, distanceMin = double.MaxValue; //find the distance values to find the distance from the connection if the foreach statement
            foreach (Connector con in connectorSet)
            {
                distance = pointFrom.DistanceTo(con.Origin);
                if (distance < distanceMin)
                {
                    distanceMin = distance;
                    connector = con;
                }
            }
            
            // Determine target point to move it to
            Transform coordinateSystem = connector.CoordinateSystem;

            Debug.Assert(
                connector.Origin.IsAlmostEqualTo( coordinateSystem.Origin ),
                "expected same origin" );

            Line line = Line.CreateUnbound( coordinateSystem.Origin, coordinateSystem.BasisZ );

            IntersectionResult intersectionResult = line.Project( pointTo );

            pointTo = intersectionResult.XYZPoint;

            Debug.Assert( line.Distance( pointTo ) < 1e-9,
                "expected projected point on line" );

            // Modify document within a transaction

            using( Transaction transaction = new Transaction( document ) )
            {
                transaction.Start( "Move Duct Connector" );
                connector.Origin = pointTo;
                transaction.Commit();
            }
            
            return Result.Succeeded;
        }
    }
}