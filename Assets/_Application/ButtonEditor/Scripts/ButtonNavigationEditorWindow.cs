#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using Uraty.Features.Button;

namespace Uraty.Application.Button.Editor
{
    public sealed class ButtonNavigationGraphEditorWindow : EditorWindow
    {
        private const float NodeWidth = 150.0f;
        private const float NodeHeight = 60.0f;
        private const float PortSize = 14.0f;
        private const float NodeSpacingX = 220.0f;
        private const float NodeSpacingY = 130.0f;
        private const float ConnectionSelectDistance = 10.0f;

        private readonly List<ButtonNavigationNode> _nodes = new();

        private ButtonNavigationNode _selectedNode;
        private ButtonNavigationNode _draggingNode;
        private ButtonNavigationNode _dragStartNode;
        private ButtonNavigationNode _selectedConnectionNode;

        private ButtonNavigationDirection _dragStartDirection;
        private ButtonNavigationDirection _selectedConnectionDirection;

        private Vector2 _dragOffset;
        private Vector2 _mousePosition;
        private Vector2 _scrollPosition;

        private bool _isDraggingConnection;
        private bool _hasSelectedConnection;
        private bool _createOppositeConnection;

        [MenuItem("Tools/Button Navigation Graph")]
        private static void Open()
        {
            GetWindow<ButtonNavigationGraphEditorWindow>("Button Navigation Graph");
        }

        private void OnEnable()
        {
            RefreshNodes();
        }

        private void OnGUI()
        {
            _mousePosition = Event.current.mousePosition;

            DrawToolbar();

            Rect graphRect = new Rect(0.0f, 22.0f, position.width, position.height - 22.0f);
            _scrollPosition = GUI.BeginScrollView(
                graphRect,
                _scrollPosition,
                new Rect(0.0f, 0.0f, 3000.0f, 2000.0f));

            DrawConnections();
            DrawDraggingConnection();
            DrawNodes();
            HandleEvents();

            GUI.EndScrollView();

            if (GUI.changed)
            {
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80.0f)))
            {
                RefreshNodes();
            }

            if (GUILayout.Button("Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(100.0f)))
            {
                AutoLayout();
            }

            _createOppositeConnection = GUILayout.Toggle(
                _createOppositeConnection,
                "Create Opposite",
                EditorStyles.toolbarButton,
                GUILayout.Width(130.0f));

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshNodes()
        {
            _nodes.Clear();

            ButtonNavigationNode[] foundNodes = FindObjectsByType<ButtonNavigationNode>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ButtonNavigationNode node in foundNodes)
            {
                if (node == null)
                {
                    continue;
                }

                _nodes.Add(node);

                if (node.EditorPosition == Vector2.zero)
                {
                    Undo.RecordObject(node, "Initialize Button Navigation Node Position");
                    node.EditorPosition = new Vector2(
                        100.0f + (_nodes.Count % 5) * NodeSpacingX,
                        100.0f + (_nodes.Count / 5) * NodeSpacingY);
                    EditorUtility.SetDirty(node);
                }
            }

            Repaint();
        }

        private void DrawNodes()
        {
            foreach (ButtonNavigationNode node in _nodes)
            {
                if (node == null)
                {
                    continue;
                }

                Rect nodeRect = GetNodeRect(node);

                GUI.Box(nodeRect, node.name, node == _selectedNode
                    ? EditorStyles.helpBox
                    : GUI.skin.box);

                foreach (ButtonNavigationDirection direction in GetDirections())
                {
                    GUI.Box(GetPortRect(node, direction), string.Empty);
                }
            }
        }

        private void DrawConnections()
        {
            Handles.BeginGUI();

            HashSet<string> drawnConnections = new();

            foreach (ButtonNavigationNode fromNode in _nodes)
            {
                if (fromNode == null)
                {
                    continue;
                }

                foreach (ButtonNavigationDirection fromDirection in GetDirections())
                {
                    ButtonNavigationNode toNode = fromNode.GetNode(fromDirection);

                    if (toNode == null)
                    {
                        continue;
                    }

                    ButtonNavigationDirection toDirection = GetOppositeDirection(fromDirection);
                    bool isBidirectional = toNode.GetNode(toDirection) == fromNode;

                    string connectionKey = GetConnectionKey(fromNode, toNode);
                    if (isBidirectional && drawnConnections.Contains(connectionKey))
                    {
                        continue;
                    }

                    drawnConnections.Add(connectionKey);

                    DrawConnection(fromNode, fromDirection, toNode, toDirection, isBidirectional);
                }
            }

            Handles.EndGUI();
        }

        private void DrawConnection(
            ButtonNavigationNode fromNode,
            ButtonNavigationDirection fromDirection,
            ButtonNavigationNode toNode,
            ButtonNavigationDirection toDirection,
            bool isBidirectional)
        {
            Vector2 startPosition = GetPortCenter(fromNode, fromDirection);
            Vector2 endPosition = GetPortCenter(toNode, toDirection);

            Handles.DrawAAPolyLine(3.0f, startPosition, endPosition);
            DrawArrowHead(startPosition, endPosition);

            if (isBidirectional)
            {
                DrawArrowHead(endPosition, startPosition);
            }
        }

        private void DrawDraggingConnection()
        {
            if (!_isDraggingConnection || _dragStartNode == null)
            {
                return;
            }

            Handles.BeginGUI();

            Vector2 startPosition = GetPortCenter(_dragStartNode, _dragStartDirection);
            Handles.DrawAAPolyLine(3.0f, startPosition, _mousePosition + _scrollPosition);

            Handles.EndGUI();
        }

        private static void DrawArrowHead(Vector2 startPosition, Vector2 endPosition)
        {
            Vector2 direction = (endPosition - startPosition).normalized;
            Vector2 right = Quaternion.Euler(0.0f, 0.0f, 25.0f) * -direction;
            Vector2 left = Quaternion.Euler(0.0f, 0.0f, -25.0f) * -direction;

            Handles.DrawAAPolyLine(3.0f, endPosition, endPosition + right * 16.0f);
            Handles.DrawAAPolyLine(3.0f, endPosition, endPosition + left * 16.0f);
        }

        private void HandleEvents()
        {
            Event currentEvent = Event.current;
            Vector2 graphMousePosition = currentEvent.mousePosition + _scrollPosition;

            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Delete)
            {
                if (_isDraggingConnection)
                {
                    CancelConnectionDrag();
                }
                else
                {
                    DeleteSelected();
                }

                currentEvent.Use();
                return;
            }

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    HandleMouseDown(currentEvent, graphMousePosition);
                    break;

                case EventType.MouseDrag:
                    HandleMouseDrag(currentEvent, graphMousePosition);
                    break;

                case EventType.MouseUp:
                    HandleMouseUp(currentEvent, graphMousePosition);
                    break;
            }
        }

        private void HandleMouseDown(Event currentEvent, Vector2 graphMousePosition)
        {
            if (currentEvent.button != 0)
            {
                return;
            }

            ClearSelection();

            foreach (ButtonNavigationNode node in _nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (TryStartConnectionDrag(node, graphMousePosition))
                {
                    currentEvent.Use();
                    return;
                }

                Rect nodeRect = GetNodeRect(node);

                if (nodeRect.Contains(graphMousePosition))
                {
                    _selectedNode = node;
                    _draggingNode = node;
                    _dragOffset = graphMousePosition - node.EditorPosition;
                    currentEvent.Use();
                    return;
                }
            }

            if (TrySelectConnection(graphMousePosition))
            {
                currentEvent.Use();
            }
        }

        private bool TryStartConnectionDrag(ButtonNavigationNode node, Vector2 graphMousePosition)
        {
            foreach (ButtonNavigationDirection direction in GetDirections())
            {
                if (!GetPortRect(node, direction).Contains(graphMousePosition))
                {
                    continue;
                }

                _isDraggingConnection = true;
                _dragStartNode = node;
                _dragStartDirection = direction;
                return true;
            }

            return false;
        }

        private void HandleMouseDrag(Event currentEvent, Vector2 graphMousePosition)
        {
            if (currentEvent.button != 0)
            {
                return;
            }

            if (_isDraggingConnection)
            {
                Repaint();
                currentEvent.Use();
                return;
            }

            if (_draggingNode == null)
            {
                return;
            }

            Undo.RecordObject(_draggingNode, "Move Button Navigation Node");
            _draggingNode.EditorPosition = graphMousePosition - _dragOffset;
            EditorUtility.SetDirty(_draggingNode);

            currentEvent.Use();
        }

        private void HandleMouseUp(Event currentEvent, Vector2 graphMousePosition)
        {
            if (currentEvent.button != 0)
            {
                return;
            }

            if (_isDraggingConnection)
            {
                if (TryGetPortAtPosition(
                        graphMousePosition,
                        out ButtonNavigationNode portTargetNode,
                        out ButtonNavigationDirection _))
                {
                    ConnectNodes(_dragStartNode, _dragStartDirection, portTargetNode);
                }
                else if (TryGetNodeAtPosition(graphMousePosition, out ButtonNavigationNode nodeTargetNode))
                {
                    ConnectNodes(_dragStartNode, _dragStartDirection, nodeTargetNode);
                }

                CancelConnectionDrag();
                currentEvent.Use();
            }

            _draggingNode = null;
        }

        private bool TryGetPortAtPosition(
            Vector2 graphMousePosition,
            out ButtonNavigationNode targetNode,
            out ButtonNavigationDirection targetDirection)
        {
            foreach (ButtonNavigationNode node in _nodes)
            {
                if (node == null || node == _dragStartNode)
                {
                    continue;
                }

                foreach (ButtonNavigationDirection direction in GetDirections())
                {
                    if (!GetPortRect(node, direction).Contains(graphMousePosition))
                    {
                        continue;
                    }

                    targetNode = node;
                    targetDirection = direction;
                    return true;
                }
            }

            targetNode = null;
            targetDirection = ButtonNavigationDirection.Up;
            return false;
        }

        private bool TryGetNodeAtPosition(Vector2 graphMousePosition, out ButtonNavigationNode targetNode)
        {
            foreach (ButtonNavigationNode node in _nodes)
            {
                if (node == null || node == _dragStartNode)
                {
                    continue;
                }

                if (!GetNodeRect(node).Contains(graphMousePosition))
                {
                    continue;
                }

                targetNode = node;
                return true;
            }

            targetNode = null;
            return false;
        }

        private void ConnectNodes(
            ButtonNavigationNode fromNode,
            ButtonNavigationDirection direction,
            ButtonNavigationNode toNode)
        {
            if (fromNode == null || toNode == null || fromNode == toNode)
            {
                return;
            }

            Undo.RecordObject(fromNode, "Connect Button Navigation");
            fromNode.SetNode(direction, toNode);
            EditorUtility.SetDirty(fromNode);

            if (!_createOppositeConnection)
            {
                return;
            }

            ButtonNavigationDirection oppositeDirection = GetOppositeDirection(direction);

            Undo.RecordObject(toNode, "Connect Opposite Button Navigation");
            toNode.SetNode(oppositeDirection, fromNode);
            EditorUtility.SetDirty(toNode);
        }

        private bool TrySelectConnection(Vector2 graphMousePosition)
        {
            foreach (ButtonNavigationNode fromNode in _nodes)
            {
                if (fromNode == null)
                {
                    continue;
                }

                foreach (ButtonNavigationDirection direction in GetDirections())
                {
                    ButtonNavigationNode toNode = fromNode.GetNode(direction);

                    if (toNode == null)
                    {
                        continue;
                    }

                    Vector2 startPosition = GetPortCenter(fromNode, direction);
                    Vector2 endPosition = GetPortCenter(toNode, GetOppositeDirection(direction));

                    float distance = HandleUtility.DistancePointLine(
                        graphMousePosition,
                        startPosition,
                        endPosition);

                    if (distance > ConnectionSelectDistance)
                    {
                        continue;
                    }

                    _selectedConnectionNode = fromNode;
                    _selectedConnectionDirection = direction;
                    _hasSelectedConnection = true;
                    return true;
                }
            }

            return false;
        }

        private void DeleteSelected()
        {
            if (_hasSelectedConnection)
            {
                DeleteSelectedConnection();
                return;
            }

            if (_selectedNode != null)
            {
                DeleteSelectedNode();
            }
        }

        private void DeleteSelectedConnection()
        {
            Undo.RecordObject(_selectedConnectionNode, "Delete Button Navigation Connection");
            _selectedConnectionNode.SetNode(_selectedConnectionDirection, null);
            EditorUtility.SetDirty(_selectedConnectionNode);

            _selectedConnectionNode = null;
            _hasSelectedConnection = false;
        }

        private void DeleteSelectedNode()
        {
            ButtonNavigationNode targetNode = _selectedNode;

            foreach (ButtonNavigationNode node in _nodes)
            {
                if (node == null || node == targetNode)
                {
                    continue;
                }

                foreach (ButtonNavigationDirection direction in GetDirections())
                {
                    if (node.GetNode(direction) != targetNode)
                    {
                        continue;
                    }

                    Undo.RecordObject(node, "Clear Deleted Button Navigation Node Reference");
                    node.SetNode(direction, null);
                    EditorUtility.SetDirty(node);
                }
            }

            Undo.DestroyObjectImmediate(targetNode);
            _selectedNode = null;
            RefreshNodes();
        }

        private void AutoLayout()
        {
            if (_nodes.Count <= 0)
            {
                return;
            }

            ButtonNavigationNode rootNode = _selectedNode != null ? _selectedNode : _nodes[0];
            HashSet<ButtonNavigationNode> visitedNodes = new();

            LayoutRecursive(rootNode, new Vector2(500.0f, 400.0f), visitedNodes);
        }

        private void LayoutRecursive(
            ButtonNavigationNode node,
            Vector2 position,
            HashSet<ButtonNavigationNode> visitedNodes)
        {
            if (node == null || visitedNodes.Contains(node))
            {
                return;
            }

            visitedNodes.Add(node);

            Undo.RecordObject(node, "Auto Layout Button Navigation");
            node.EditorPosition = position;
            EditorUtility.SetDirty(node);

            foreach (ButtonNavigationDirection direction in GetDirections())
            {
                ButtonNavigationNode nextNode = node.GetNode(direction);

                if (nextNode == null)
                {
                    continue;
                }

                LayoutRecursive(nextNode, position + GetOffset(direction), visitedNodes);
            }
        }

        private void ClearSelection()
        {
            _selectedNode = null;
            _selectedConnectionNode = null;
            _hasSelectedConnection = false;
        }

        private void CancelConnectionDrag()
        {
            _isDraggingConnection = false;
            _dragStartNode = null;
            Repaint();
        }

        private Rect GetNodeRect(ButtonNavigationNode node)
        {
            return new Rect(node.EditorPosition.x, node.EditorPosition.y, NodeWidth, NodeHeight);
        }

        private Rect GetPortRect(ButtonNavigationNode node, ButtonNavigationDirection direction)
        {
            Vector2 center = GetPortCenter(node, direction);

            return new Rect(
                center.x - PortSize * 0.5f,
                center.y - PortSize * 0.5f,
                PortSize,
                PortSize);
        }

        private Vector2 GetPortCenter(ButtonNavigationNode node, ButtonNavigationDirection direction)
        {
            Rect nodeRect = GetNodeRect(node);

            return direction switch
            {
                ButtonNavigationDirection.Up => new Vector2(nodeRect.center.x, nodeRect.yMin),
                ButtonNavigationDirection.Down => new Vector2(nodeRect.center.x, nodeRect.yMax),
                ButtonNavigationDirection.Left => new Vector2(nodeRect.xMin, nodeRect.center.y),
                ButtonNavigationDirection.Right => new Vector2(nodeRect.xMax, nodeRect.center.y),
                _ => nodeRect.center
            };
        }

        private static Vector2 GetOffset(ButtonNavigationDirection direction)
        {
            return direction switch
            {
                ButtonNavigationDirection.Up => new Vector2(0.0f, -NodeSpacingY),
                ButtonNavigationDirection.Down => new Vector2(0.0f, NodeSpacingY),
                ButtonNavigationDirection.Left => new Vector2(-NodeSpacingX, 0.0f),
                ButtonNavigationDirection.Right => new Vector2(NodeSpacingX, 0.0f),
                _ => Vector2.zero
            };
        }

        private static ButtonNavigationDirection GetOppositeDirection(
            ButtonNavigationDirection direction)
        {
            return direction switch
            {
                ButtonNavigationDirection.Up => ButtonNavigationDirection.Down,
                ButtonNavigationDirection.Down => ButtonNavigationDirection.Up,
                ButtonNavigationDirection.Left => ButtonNavigationDirection.Right,
                ButtonNavigationDirection.Right => ButtonNavigationDirection.Left,
                _ => ButtonNavigationDirection.Down
            };
        }

        private static IEnumerable<ButtonNavigationDirection> GetDirections()
        {
            yield return ButtonNavigationDirection.Up;
            yield return ButtonNavigationDirection.Down;
            yield return ButtonNavigationDirection.Left;
            yield return ButtonNavigationDirection.Right;
        }

        private static string GetConnectionKey(ButtonNavigationNode nodeA, ButtonNavigationNode nodeB)
        {
            int idA = nodeA.GetInstanceID();
            int idB = nodeB.GetInstanceID();

            return idA < idB ? $"{idA}_{idB}" : $"{idB}_{idA}";
        }
    }
}
#endif
