﻿using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace Dock.Model
{
    /// <summary>
    /// Docking manager.
    /// </summary>
    public class DockManager : IDockManager
    {
        /// <summary>
        /// Gets or sets dock manager settings.
        /// </summary>
        private static DockManagerSettings Settings { get; set; } = new();

        /// <summary>
        /// Set dock manager settings.
        /// </summary>
        /// <param name="settings">The dock manager settings.</param>
        public static void SetSettings(DockManagerSettings settings) => Settings = settings;

        /// <inheritdoc/>
        public DockPoint Position { get; set; }

        /// <inheritdoc/>
        public DockPoint ScreenPosition { get; set; }

        private bool MoveDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, bool bExecute)
        {
            if (sourceDockableOwner == targetDock)
            {
                if (targetDock.VisibleDockables?.Count == 1)
                {
                    return false;
                }
            }
            var targetDockable = targetDock.ActiveDockable;
            if (targetDockable is null)
            {
                targetDockable = targetDock.VisibleDockables?.LastOrDefault();
                if (targetDockable is null)
                {
                    if (bExecute)
                    {
                        if (sourceDockableOwner.Factory is { } factory)
                        {
                            factory.MoveDockable(sourceDockableOwner, targetDock, sourceDockable, null);
                        }
                    }
                    return true;
                }
            }
            if (bExecute)
            {
                if (sourceDockableOwner.Factory is { } factory)
                {
                    factory.MoveDockable(sourceDockableOwner, targetDock, sourceDockable, targetDockable);
                }
            }
            return true;
        }

        private bool SwapDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, bool bExecute)
        {
            var targetDockable = targetDock.ActiveDockable;
            if (targetDockable is null)
            {
                targetDockable = targetDock.VisibleDockables?.LastOrDefault();
                if (targetDockable is null)
                {
                    return false;
                }
            }
            if (bExecute)
            {
                if (sourceDockableOwner.Factory is { } factory)
                {
                    factory.SwapDockable(sourceDockableOwner, targetDock, sourceDockable, targetDockable);
                }
            }
            return true;
        }

        private void SplitToolDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, DockOperation operation)
        {
            if (targetDock.Factory is { } factory)
            {
                IToolDock toolDock = factory.CreateToolDock();
                toolDock.Id = nameof(IToolDock);
                toolDock.Title = nameof(IToolDock);
                toolDock.VisibleDockables = factory.CreateList<IDockable>();
                factory.MoveDockable(sourceDockableOwner, toolDock, sourceDockable, null);
                factory.SplitToDock(targetDock, toolDock, operation);
            }
        }

        private void SplitDocumentDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, DockOperation operation)
        {
            if (targetDock.Factory is { } factory)
            {
                IDocumentDock documentDock = factory.CreateDocumentDock();
                documentDock.Id = nameof(IDocumentDock);
                documentDock.Title = nameof(IDocumentDock);
                documentDock.VisibleDockables = factory.CreateList<IDockable>();
                if (sourceDockableOwner is IDocumentDock sourceDocumentDock)
                {
                    documentDock.CanCreateDocument = sourceDocumentDock.CanCreateDocument;
                }
                factory.MoveDockable(sourceDockableOwner, documentDock, sourceDockable, null);
                factory.SplitToDock(targetDock, documentDock, operation);
            }
        }

        private bool SplitDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, DockOperation operation, bool bExecute)
        {
            switch (sourceDockable)
            {
                case ITool _:
                    if (sourceDockableOwner == targetDock)
                    {
                        if (targetDock.VisibleDockables?.Count == 1)
                        {
                            return false;
                        }
                    }
                    if (bExecute)
                    {
                        SplitToolDockable(sourceDockable, sourceDockableOwner, targetDock, operation);
                    }
                    return true;
                case IDocument _:
                    if (sourceDockableOwner == targetDock)
                    {
                        if (targetDock.VisibleDockables?.Count == 1)
                        {
                            return false;
                        }
                    }
                    if (bExecute)
                    {
                        SplitDocumentDockable(sourceDockable, sourceDockableOwner, targetDock, operation);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private bool DockDockableIntoWindow(IDockable sourceDockable, IDockable targetDockable, DockOperation operation, bool bExecute)
        {
            switch (operation)
            {
                case DockOperation.Window:
                    if (sourceDockable == targetDockable)
                    {
                        return false;
                    }

                    if (sourceDockable.Owner is not IDock sourceDockableOwner)
                    {
                        return false;
                    }

                    if (sourceDockableOwner.Factory is not { } factory)
                    {
                        return false;
                    }
                    
                    if (factory.FindRoot(sourceDockable, _ => true) is {ActiveDockable: IDock targetWindowOwner})
                    {
                        if (bExecute)
                        {
                            factory.SplitToWindow(
                                targetWindowOwner, 
                                sourceDockable, 
                                ScreenPosition.X, 
                                ScreenPosition.Y, 
                                Settings.SplitWindowWidth, 
                                Settings.SplitWindowHeight);
                        }
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private bool DockDockableIntoDockable(IDockable sourceDockable, IDockable targetDockable, DragAction action, bool bExecute)
        {
            if (sourceDockable.Owner is IDock sourceDockableOwner && targetDockable.Owner is IDock targetDockableOwner)
            {
                if (sourceDockableOwner == targetDockableOwner)
                {
                    return DockDockable(sourceDockable, sourceDockableOwner, targetDockable, action, bExecute);
                }

                return DockDockable(sourceDockable, sourceDockableOwner, targetDockable, targetDockableOwner, action, bExecute);
            }
            return false;
        }

        private bool DockDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDockable targetDockable, DragAction action, bool bExecute)
        {
            switch (action)
            {
                case DragAction.Copy:
                    return false;
                case DragAction.Move:
                    if (bExecute)
                    {
                        if (sourceDockableOwner.Factory is { } factory)
                        {
                            factory.MoveDockable(sourceDockableOwner, sourceDockable, targetDockable);
                        }
                    }
                    return true;
                case DragAction.Link:
                    if (bExecute)
                    {
                        if (sourceDockableOwner.Factory is { } factory)
                        {
                            factory.SwapDockable(sourceDockableOwner, sourceDockable, targetDockable);
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        private bool DockDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDockable targetDockable, IDock targetDockableOwner, DragAction action, bool bExecute)
        {
            switch (action)
            {
                case DragAction.Copy:
                    return false;
                case DragAction.Move:
                    if (bExecute)
                    {
                        if (sourceDockableOwner.Factory is { } factory)
                        {
                            factory.MoveDockable(sourceDockableOwner, targetDockableOwner, sourceDockable, targetDockable);
                        }
                    }
                    return true;
                case DragAction.Link:
                    if (bExecute)
                    {
                        if (sourceDockableOwner.Factory is { } factory)
                        {
                            factory.SwapDockable(sourceDockableOwner, targetDockableOwner, sourceDockable, targetDockable);
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        private bool DockDockable(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, DockOperation operation, bool bExecute)
        {
            return operation switch
            {
                DockOperation.Fill => MoveDockable(sourceDockable, sourceDockableOwner, targetDock, bExecute),
                DockOperation.Left => SplitDockable(sourceDockable, sourceDockableOwner, targetDock, operation, bExecute),
                DockOperation.Right => SplitDockable(sourceDockable, sourceDockableOwner, targetDock, operation, bExecute),
                DockOperation.Top => SplitDockable(sourceDockable, sourceDockableOwner, targetDock, operation, bExecute),
                DockOperation.Bottom => SplitDockable(sourceDockable, sourceDockableOwner, targetDock, operation, bExecute),
                DockOperation.Window => DockDockableIntoWindow(sourceDockable, targetDock, operation, bExecute),
                _ => false
            };
        }

        private bool DockDockableIntoDock(IDockable sourceDockable, IDock targetDock, DragAction action, DockOperation operation, bool bExecute)
        {
            if (sourceDockable.Owner is IDock sourceDockableOwner)
            {
                return DockDockableIntoDock(sourceDockable, sourceDockableOwner, targetDock, action, operation, bExecute);
            }
            return false;
        }

        private bool DockDockableIntoDock(IDockable sourceDockable, IDock sourceDockableOwner, IDock targetDock, DragAction action, DockOperation operation, bool bExecute)
        {
            return action switch
            {
                DragAction.Copy => false,
                DragAction.Move => DockDockable(sourceDockable, sourceDockableOwner, targetDock, operation, bExecute),
                DragAction.Link => SwapDockable(sourceDockable, sourceDockableOwner, targetDock, bExecute),
                _ => false
            };
        }

        private bool DockDockableIntoDockVisible(IDock sourceDock, IDock targetDock, DragAction action, DockOperation operation, bool bExecute)
        {
            var visible = sourceDock.VisibleDockables?.ToList();
            if (visible is not null)
            {
                foreach (var dockable in visible)
                {
                    if (DockDockableIntoDock(dockable, targetDock, action, operation, bExecute) == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool DockDockIntoDock(IDock sourceDock, IDock targetDock, DragAction action, DockOperation operation, bool bExecute)
        {
            var visible = sourceDock.VisibleDockables?.ToList();
            if (visible is null)
            {
                return true;
            }
            
            if (visible.Count == 1)
            {
                if (DockDockableIntoDock(visible.First(), targetDock, action, operation, bExecute) == false)
                {
                    return false;
                }
            }
            else
            {
                if (DockDockableIntoDock(visible.First(), targetDock, action, operation, bExecute) == false)
                {
                    return false;
                }
                
                foreach (var dockable in visible.Skip(1))
                {
                    if (DockDockableIntoDockable(dockable, visible.First(), action, bExecute) == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool DockDockable(IDock sourceDock, IDockable targetDockable, IDock targetDock, DragAction action, DockOperation operation, bool bExecute)
        {
            return operation switch
            {
                DockOperation.Fill => DockDockableIntoDockVisible(sourceDock, targetDock, action, operation, bExecute),
                DockOperation.Window => DockDockableIntoWindow(sourceDock, targetDockable, operation, bExecute),
                _ => DockDockIntoDock(sourceDock, targetDock, action, operation, bExecute)
            };
        }

        /// <inheritdoc/>
        public bool ValidateTool(ITool sourceTool, IDockable targetDockable, DragAction action, DockOperation operation, bool bExecute)
        {
            return targetDockable switch
            {
                IRootDock _ => DockDockableIntoWindow(sourceTool, targetDockable, operation, bExecute),
                IToolDock toolDock => DockDockableIntoDock(sourceTool, toolDock, action, operation, bExecute),
                IDocumentDock documentDock => DockDockableIntoDock(sourceTool, documentDock, action, operation, bExecute),
                ITool tool => DockDockableIntoDockable(sourceTool, tool, action, bExecute),
                IDocument document => DockDockableIntoDockable(sourceTool, document, action, bExecute),
                _ => false
            };
        }

        /// <inheritdoc/>
        public bool ValidateDocument(IDocument sourceDocument, IDockable targetDockable, DragAction action, DockOperation operation, bool bExecute)
        {
            return targetDockable switch
            {
                IRootDock _ => DockDockableIntoWindow(sourceDocument, targetDockable, operation, bExecute),
                IDocumentDock documentDock => DockDockableIntoDock(sourceDocument, documentDock, action, operation, bExecute),
                IDocument document => DockDockableIntoDockable(sourceDocument, document, action, bExecute),
                _ => false
            };
        }

        /// <inheritdoc/>
        public bool ValidateDock(IDock sourceDock, IDockable targetDockable, DragAction action, DockOperation operation, bool bExecute)
        {
            return targetDockable switch
            {
                IRootDock _ => DockDockableIntoWindow(sourceDock, targetDockable, operation, bExecute),
                IToolDock toolDock => sourceDock != toolDock &&
                                      DockDockable(sourceDock, targetDockable, toolDock, action, operation, bExecute),
                IDocumentDock documentDock => sourceDock != documentDock &&
                                              DockDockable(sourceDock, targetDockable, documentDock, action, operation, bExecute),
                _ => false
            };
        }

        /// <inheritdoc/>
        public bool ValidateDockable(IDockable sourceDockable, IDockable targetDockable, DragAction action, DockOperation operation, bool bExecute)
        {
            return sourceDockable switch
            {
                IToolDock toolDock => ValidateDock(toolDock, targetDockable, action, operation, bExecute),
                IDocumentDock documentDock => ValidateDock(documentDock, targetDockable, action, operation, bExecute),
                ITool tool => ValidateTool(tool, targetDockable, action, operation, bExecute),
                IDocument document => ValidateDocument(document, targetDockable, action, operation, bExecute),
                _ => false
            };
        }
    }
}
