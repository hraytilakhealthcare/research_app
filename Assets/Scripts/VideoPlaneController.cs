using UnityEngine;

public class VideoPlaneController : MonoBehaviour
{
    public Tracker Tracker;
    private float aspect;
    private float texCoordX;
    private float texCoordY;
    private float yScale;
    private float xScale;

    void Update()
    {
        Vector2Int imageSize = VisageTrackerApi.LastCameraInfo.ImageSize;
        if (Tracker != null && imageSize.x != 0 && imageSize.y != 0)
        {
            // Get mesh filter
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Vector2[] uv = meshFilter.mesh.uv;
            Vector2[] uv2 = meshFilter.mesh.uv2;

            // Calculate texture coordinates for the 
            // video frame part of the texture (texCoordX, texCoordY)
            //
            //   <------<TexWidth>------>
            // <ImageWidth>
            // uv[0]---uv[3]---------------
            // |          |   ^           |     ^
            // |          |   |           |     |
            // |          | <ImageHeight> |     |
            // |          |   |           |     |
            // |          |   v           |     |
            // uv[1]---uv[2]              |<TexHeight>
            // |           <texCoordX,    |     |
            // |           texCoordY,>    |     |
            // |                          |     |   
            // |                          |     v
            // ----------------------------

            // Calculate uv[2] texture coordinates
            texCoordX = imageSize.x / (float) Tracker.TexWidth;
            texCoordY = imageSize.y / (float) Tracker.TexHeight;

            // Apply new coordinates
            uv[1].y = texCoordY;
            uv[2].x = texCoordX;
            uv[2].y = texCoordY;
            uv[3].x = texCoordX;

            meshFilter.mesh.uv = uv;
            meshFilter.mesh.uv2 = uv2;

            // Adjust texture scale so the frame fits
            // Fixate so the the frame always fits by height
            aspect = imageSize.x / (float) imageSize.y;
            yScale = 100.0f;
            xScale = yScale * aspect;
            // NOTE: due to rotation on the VideoPlane yScale is applied on z coordinate
        }

        gameObject.transform.localScale = VisageTrackerApi.IsMirrored
            ? new Vector3(-xScale, 1.0f, yScale)
            : new Vector3(xScale, 1.0f, yScale);
    }
}
