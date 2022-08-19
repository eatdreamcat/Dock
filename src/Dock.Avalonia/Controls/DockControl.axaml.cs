﻿using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Dock.Avalonia.Internal;
using Dock.Model;
using Dock.Model.Core;

namespace Dock.Avalonia.Controls;

/// <summary>
/// Interaction logic for <see cref="DockControl"/> xaml.
/// </summary>
public class DockControl : TemplatedControl, IDockControl
{
    private readonly DockManager _dockManager;
    private readonly DockControlState _dockControlState;
    private bool _isInitialized;

    /// <summary>
    /// Defines the <see cref="Layout"/> property.
    /// </summary>
    public static readonly StyledProperty<IDock?> LayoutProperty =
        AvaloniaProperty.Register<DockControl, IDock?>(nameof(Layout));

    /// <summary>
    /// Defines the <see cref="InitializeLayout"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> InitializeLayoutProperty =
        AvaloniaProperty.Register<DockControl, bool>(nameof(InitializeLayout));

    /// <summary>
    /// Defines the <see cref="InitializeFactory"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> InitializeFactoryProperty =
        AvaloniaProperty.Register<DockControl, bool>(nameof(InitializeFactory));

    /// <summary>
    /// Defines the <see cref="FactoryType"/> property.
    /// </summary>
    public static readonly StyledProperty<Type?> FactoryTypeProperty =
        AvaloniaProperty.Register<DockControl, Type?>(nameof(FactoryType));

    /// <inheritdoc/>
    public IDockManager DockManager => _dockManager;

    /// <inheritdoc/>
    public IDockControlState DockControlState => _dockControlState;

    /// <inheritdoc/>
    [Content]
    public IDock? Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <inheritdoc/>
    public bool InitializeLayout
    {
        get => GetValue(InitializeLayoutProperty);
        set => SetValue(InitializeLayoutProperty, value);
    }

    /// <inheritdoc/>
    public bool InitializeFactory
    {
        get => GetValue(InitializeFactoryProperty);
        set => SetValue(InitializeFactoryProperty, value);
    }

    /// <inheritdoc/>
    public Type? FactoryType
    {
        get => GetValue(FactoryTypeProperty);
        set => SetValue(FactoryTypeProperty, value);
    }

    /// <summary>
    /// Initialize the new instance of the <see cref="DockControl"/>.
    /// </summary>
    public DockControl()
    {
        _dockManager = new DockManager();
        _dockControlState = new DockControlState(_dockManager);
        AddHandler(PointerPressedEvent, PressedHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, ReleasedHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, MovedHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerEnterEvent, EnterHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerLeaveEvent, LeaveHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerCaptureLostEvent, CaptureLostHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerWheelChangedEvent, WheelChangedHandler, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LayoutProperty)
        {
            if (_isInitialized)
            {
                DeInitialize(change.OldValue.GetValueOrDefault<IDock>());
            }

            Initialize(change.NewValue.GetValueOrDefault<IDock>()); 
        }
    }

    private void Initialize(IDock? layout)
    {
        if (layout is null)
        {
            return;
        }

        if (layout.Factory is null)
        {
            if (FactoryType is { })
            {
                var factory = (IFactory?)Activator.CreateInstance(FactoryType);
                if (factory is { })
                {
                    layout.Factory = factory;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        layout.Factory.DockControls.Add(this);

        if (InitializeFactory)
        {
            layout.Factory.ContextLocator = new Dictionary<string, Func<object>>();
            layout.Factory.HostWindowLocator = new Dictionary<string, Func<IHostWindow>>
            {
                [nameof(IDockWindow)] = () => new HostWindow()
            };
            layout.Factory.DockableLocator = new Dictionary<string, Func<IDockable?>>();
            layout.Factory.DefaultContextLocator = GetContext;
            layout.Factory.DefaultHostWindowLocator = GetHostWindow;
 
            IHostWindow GetHostWindow() => new HostWindow();
            object GetContext() => layout;
        }

        if (InitializeLayout)
        {
            layout.Factory.InitLayout(layout);
        }

        _isInitialized = true;
    }

    private void DeInitialize(IDock? layout)
    {
        if (layout?.Factory is null)
        {
            return;
        }

        layout.Factory.DockControls.Remove(this);

        if (InitializeLayout)
        {
            if (layout.Close.CanExecute(null))
            {
                layout.Close.Execute(null);
            }
        }

        _isInitialized = false;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_isInitialized)
        {
            DeInitialize(Layout);
        }

        Initialize(Layout);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_isInitialized)
        {
            DeInitialize(Layout);
        }
    }

    private static DragAction ToDragAction(PointerEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return DragAction.Link;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return DragAction.Move;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return DragAction.Copy;
        }

        return DragAction.Move;
    }

    private void PressedHandler(object? sender, PointerPressedEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = e.GetPosition(this);
            var delta = new Vector();
            var action = ToDragAction(e);
            _dockControlState.Process(position, delta, EventType.Pressed, action, this, Layout.Factory.DockControls);
        }
    }

    private void ReleasedHandler(object? sender, PointerReleasedEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = e.GetPosition(this);
            var delta = new Vector();
            var action = ToDragAction(e);
            _dockControlState.Process(position, delta, EventType.Released, action, this, Layout.Factory.DockControls);
        }
    }

    private void MovedHandler(object? sender, PointerEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = e.GetPosition(this);
            var delta = new Vector();
            var action = ToDragAction(e);
            _dockControlState.Process(position, delta, EventType.Moved, action, this, Layout.Factory.DockControls);
        }
    }

    private void EnterHandler(object? sender, PointerEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = e.GetPosition(this);
            var delta = new Vector();
            var action = ToDragAction(e);
            _dockControlState.Process(position, delta, EventType.Enter, action, this, Layout.Factory.DockControls);
        }
    }

    private void LeaveHandler(object? sender, PointerEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = e.GetPosition(this);
            var delta = new Vector();
            var action = ToDragAction(e);
            _dockControlState.Process(position, delta, EventType.Leave, action, this, Layout.Factory.DockControls);
        }
    }

    private void CaptureLostHandler(object? sender, PointerCaptureLostEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = new Point();
            var delta = new Vector();
            var action = DragAction.None;
            _dockControlState.Process(position, delta, EventType.CaptureLost, action, this, Layout.Factory.DockControls);
        }
    }

    private void WheelChangedHandler(object? sender, PointerWheelEventArgs e)
    {
        if (Layout?.Factory?.DockControls is { })
        {
            var position = e.GetPosition(this);
            var delta = e.Delta;
            var action = ToDragAction(e);
            _dockControlState.Process(position, delta, EventType.WheelChanged, action, this, Layout.Factory.DockControls);
        }
    }
}
