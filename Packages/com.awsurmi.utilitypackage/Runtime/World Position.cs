using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public static class WorldPosition
{
    public static Vector3 GetMouseWorldPosition ()
    {
        Vector3 mousePosition = GetMouseWorldPosition (Pointer.current.position.ReadValue (), Camera.main);
        mousePosition.z = 0;

        return mousePosition;
    }

    public static Vector3 GetMouseWorldPosition (Camera worldCamera)
    {
        Vector3 mousePosition = GetMouseWorldPosition (Pointer.current.position.ReadValue (), worldCamera);
        mousePosition.z = 0;

        return mousePosition;
    }

    public static Vector3 GetMouseWorldPositionWithZ ()
    {
        return GetMouseWorldPosition (Pointer.current.position.ReadValue (), Camera.main);
    }

    private static Vector3 GetMouseWorldPosition (Vector3 screenPosition, Camera worldCamera)
    {
        return worldCamera.ScreenToWorldPoint (screenPosition);
    }

    public static void MoveCameraIn3DWorldSpace (Camera camera, float moveSpeed)
    {
        if (!camera || Keyboard.current == null) return;

        Vector3 moveDirection = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) moveDirection += Vector3.forward;
        if (Keyboard.current.aKey.isPressed) moveDirection += Vector3.right;
        if (Keyboard.current.sKey.isPressed) moveDirection += Vector3.back;
        if (Keyboard.current.dKey.isPressed) moveDirection += Vector3.left;

        if (moveDirection != Vector3.zero) moveDirection.Normalize ();

        camera.transform.position += moveDirection * (moveSpeed * Time.deltaTime);
    }

    public static void MoveCameraIn2DWorldSpace (Camera camera, float moveSpeed)
    {
        if (!camera || Keyboard.current == null) return;

        Vector3 moveDirection = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) moveDirection += Vector3.up;
        if (Keyboard.current.aKey.isPressed) moveDirection += Vector3.right;
        if (Keyboard.current.sKey.isPressed) moveDirection += Vector3.down;
        if (Keyboard.current.dKey.isPressed) moveDirection += Vector3.left;

        if (moveDirection != Vector3.zero) moveDirection.Normalize ();

        camera.transform.position += moveDirection * (moveSpeed * Time.deltaTime);
    }

    public static void MoveCameraWithMouseIn3DWorldSpace (Camera camera, float moveSpeed)
    {
        if (!camera || Pointer.current == null) return;

        Vector2 pointerPosition = Pointer.current.position.ReadValue ();
        
        Vector3 moveDirection = new (pointerPosition.x, 0f, pointerPosition.y);
        moveDirection.Normalize ();

        camera.transform.position += moveDirection * (moveSpeed * Time.deltaTime);
    }

    public static void MoveCameraWithMouseIn2DWorldSpace (Camera camera, float moveSpeed)
    {
        if (!camera || Mouse.current == null) return;
        if (!Mouse.current.rightButton.isPressed) return;

        Vector2 mouseDelta = -Mouse.current.delta.ReadValue ();
        Vector3 moveDirection = new (mouseDelta.x, mouseDelta.y, 0f);
        moveDirection.Normalize ();

        if (moveDirection != Vector3.zero) moveDirection.Normalize ();

        camera.transform.position += moveDirection * (moveSpeed * Time.deltaTime);
    }
}

public static class WorldText
{
    public static TextMeshPro CreateWorldText (
        string text,
        int fontSize = 10,
        Vector3 localPosition = default,
        Transform parent = null,
        Vector2 containerSize = default,
        TextAlignmentOptions textAlignmentOptions = TextAlignmentOptions.Center,
        bool autoTextContainerSize = false,
        Color? color = null,
        int sortingOrder = 0)
    {
        TextMeshPro textMeshPro = new GameObject ("World Text", typeof (TextMeshPro)).GetComponent <TextMeshPro> ();
        textMeshPro.transform.SetParent (parent);
        textMeshPro.transform.position = localPosition;
            
        textMeshPro.SetText (text);
            
        if (containerSize != default) textMeshPro.rectTransform.sizeDelta = containerSize;
        textMeshPro.autoSizeTextContainer = autoTextContainerSize;
        textMeshPro.alignment = textAlignmentOptions;
        textMeshPro.fontSize = fontSize;
        textMeshPro.color = color ?? Color.white;
        if (sortingOrder != 0) textMeshPro.GetComponent <MeshRenderer> ().sortingOrder = sortingOrder;
        
        return textMeshPro;
    }
}