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
    public class CommandReconnect : IExternalCommand
    {
        static Connector GetConnectorConnector(Connector connector)
            {
                Connector neighbour = null;
                int ownerId = connector.Owner.Id.IntegerValue;
                ConnectorSet references = connector.AllRefs;
                foreach (Connector conn in references)
                {
                    //Ignore non-End connectors and connectors on the same element
                    if (conn.ConnectorType == ConnectorType.End && !ownerId.Equals(conn.Owner.Id.IntegerValue))
                    {
                        neighbour = conn;
                        break;
                    }
                }

                return neighbour;
            }

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

            // Try to select both duct objects 
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
                return Result.Cancelled;
            }
            
            // determine connector closet to picked point
            ConnectorSet connectors = duct.ConnectorManager.Connectors;

            Connector connector = null;
            double distance, distanceMin = double.MaxValue;

            foreach (Connector conn in connectors)
            {
                distance = pointFrom.DistanceTo(conn.Origin);
                if (distance < distanceMin)
                {
                    distanceMin = distance;
                    connector = conn;
                }
            }
            
            // Determine target point to move it to
            Transform coordinateSystem = connector.CoordinateSystem;
            Debug.Assert(connector.Origin.IsAlmostEqualTo(coordinateSystem.Origin),
                "expected same origin");

            Line line = Line.CreateBound(coordinateSystem.Origin, coordinateSystem.BasisZ);
            IntersectionResult intersectionResult = line.Project(pointTo);
            pointTo = intersectionResult.XYZPoint;
            
            Debug.Assert(line.Distance(pointTo) < 1e-9,
                "expected projected point oin line");
            
            // Determine the translation Vector
            XYZ vector = pointTo - pointFrom;
            
            // Determine neighbouring fitting
            Connector neighbour = GetConnectorConnector(connector);
            
            // Modify document within a transaction
            using (Transaction transaction = new Transaction(document))
            {
                transaction.Start("Move Fitting");
                ElementTransformUtils.MoveElement(document, neighbour.Owner.Id, vector);
                transaction.Commit();
            }
            return Result.Succeeded;
        }
    }
}