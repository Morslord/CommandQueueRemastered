using UnityEngine;
using UnityEngine.UI;

namespace CommandQueueRemastered.Game
{
    // Snap the rendered geometry only; leave icon bounds, layout and scrolling unchanged.
    internal sealed class PixelAlignedStrip : MaskableGraphic
    {
        internal bool AlignTop;
        private Rect renderedPixels;
        private Matrix4x4 renderedTransform;
        private readonly Vector3[] corners = new Vector3[4];
        private Camera RenderCamera => canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;

        private Rect PixelBounds()
        {
            rectTransform.GetWorldCorners(corners);
            var lower = RectTransformUtility.WorldToScreenPoint(RenderCamera, corners[0]);
            var upper = RectTransformUtility.WorldToScreenPoint(RenderCamera, corners[2]);
            // Prefer the smaller strip: fractional heights must not add a row on some tiles.
            float height = Mathf.Max(1, Mathf.Floor(upper.y - lower.y + 0.001f));
            float bottom = AlignTop ? Mathf.Round(upper.y) - height : Mathf.Round(lower.y);
            return Rect.MinMaxRect(Mathf.Round(lower.x), bottom, Mathf.Round(upper.x), bottom + height);
        }

        private void LateUpdate()
        {
            // Parent scaling/movement need not change this RectTransform's local dimensions.
            if (renderedTransform != rectTransform.localToWorldMatrix || renderedPixels != PixelBounds()) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!canvas) return;
            renderedPixels = PixelBounds(); renderedTransform = rectTransform.localToWorldMatrix;
            var camera = RenderCamera;
            AddVertex(mesh, new Vector2(renderedPixels.xMin, renderedPixels.yMin), camera);
            AddVertex(mesh, new Vector2(renderedPixels.xMin, renderedPixels.yMax), camera);
            AddVertex(mesh, new Vector2(renderedPixels.xMax, renderedPixels.yMax), camera);
            AddVertex(mesh, new Vector2(renderedPixels.xMax, renderedPixels.yMin), camera);
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
        }

        private void AddVertex(VertexHelper mesh, Vector2 pixel, Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pixel, camera, out var local);
            var vertex = UIVertex.simpleVert; vertex.position = local; vertex.color = color;
            mesh.AddVert(vertex);
        }
    }
}
