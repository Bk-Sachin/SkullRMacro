using System.Windows;
using System.Windows.Media;

namespace SkullRMacro
{
    public static class MousePartProperties
    {
        // Define the attached property
        public static readonly DependencyProperty PathDataProperty =
            DependencyProperty.RegisterAttached(
                "PathData", // Property name
                typeof(Geometry), // Property type
                typeof(MousePartProperties), // Owner type
                new PropertyMetadata(null) // Default value
            );

        // Getter
        public static Geometry GetPathData(DependencyObject obj)
        {
            return (Geometry)obj.GetValue(PathDataProperty);
        }

        // Setter
        public static void SetPathData(DependencyObject obj, Geometry value)
        {
            obj.SetValue(PathDataProperty, value);
        }
    }
}