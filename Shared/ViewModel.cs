using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
#if DLL
using Invert.MVVM;
using Invert.uFrame.Editor;
namespace Invert.MVVM
{
#endif
using UniRx;

/// <summary>
///  A data structure that contains information/data needed for a 'View'
/// </summary>
[Serializable]
public abstract class ViewModel
#if !DLL
    :  IUFSerializable, INotifyPropertyChanged , IObservable<IObservableProperty>, IDisposable, IBindable
#else
 : INotifyPropertyChanged
#endif
{
    [Obsolete]
    public bool Dirty { get; set; }
    public event PropertyChangedEventHandler PropertyChanged;
    private Dictionary<int, List<IDisposable>> _bindings;
    private Controller _controller;
    private List<ViewModelPropertyInfo> _modelProperties;
    private string _identifier;

    protected ViewModel()
    {
#if !UNITY_EDITOR
        BindInternal();
#endif
    }

    protected IEventAggregator Aggregator { get; set; }

    protected ViewModel(IEventAggregator aggregator)
    {
        if (aggregator == null) throw new ArgumentNullException("aggregator");
        Aggregator = aggregator;
        BindInternal();
    }

    public virtual void MethodAccessException()
    {
        
    }
    private void BindInternal()
    {
        if (!_isBound)
        {
            Bind();
            _isBound = true;
        }

    }
    [Obsolete("Use new ViewModel(EventAggregator) instead.")]
    protected ViewModel(Controller controller, bool initialize = true) :this(controller.EventAggregator)
    {
      
    }

    /// <summary>
    ///Access a model property via string.  This is optimized using a compiled delegate to
    ///access derived classes properties so use as needed
    /// </summary>
    /// <param name="bindingPropertyName">The name of the property/field to access</param>
    /// <returns>ModelPropertyBase The Model Property class.  Use value to get the value of the property</returns>
    public ViewModelPropertyInfo this[string bindingPropertyName]
    {
        get
        {
            try
            {
                CacheReflectedModelProperties();
                return _modelProperties.FirstOrDefault(p=>p.Property.PropertyName == bindingPropertyName);
            }
            catch (Exception ex)
            {
                throw new Exception(bindingPropertyName + " was not found on " + this.ToString() + ": " + ex.Message);
            }
        }
    }

    public Dictionary<int, List<IDisposable>> Bindings
    {
        get { return _bindings ?? (_bindings = new Dictionary<int, List<IDisposable>>()); }
        set { _bindings = value; }
    }


    [Obsolete("Controllers are no longer needed on viewmodels.")]
    public Controller Controller {
        get
        {
            throw new Exception("You should not be accessing controllers from the viewmodel.  It also obsolete in 1.6");
        }
        set
        {
            
        } }

    private bool _isBound;
    public virtual void Bind()
    {
        
    }

    public virtual string Identifier
    {
        get { return _identifier; }
        set { _identifier = value; }
    }

    public List<ViewModelPropertyInfo> Properties
    {
        get
        {
            CacheReflectedModelProperties();
            return _modelProperties;
        }
    }

    public int References { get; set; }

    public IDisposable AddBinding(IDisposable binding)
    {
        if (!Bindings.ContainsKey(-1))
        {
            Bindings[-1] = new List<IDisposable>();
        }
        Bindings[-1].Add(binding);
        return binding;
    }

    /// <summary>
    /// Reflection-less get of all view-model commands generated by the designer tool.
    /// </summary>
    /// <returns></returns>
    public List<ViewModelCommandInfo> GetViewModelCommands()
    {
        var list = new List<ViewModelCommandInfo>();
        FillCommands(list);
        return list;
    }

    /// <summary>
    /// Reflection-less get of all view-model commands generated by the designer tool.
    /// </summary>
    /// <returns></returns>
    public List<ViewModelPropertyInfo> GetViewModelProperties()
    {
        var list = new List<ViewModelPropertyInfo>();
        FillProperties(list);
        return list;
    }

    /// <summary>
    /// Implementation of Microsoft's INotifyPropertyChanged
    /// </summary>
    /// <param name="propertyName"></param>
    public virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Implementation of Microsoft's INotifyPropertyChanged
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="propertyName"></param>
    public virtual void OnPropertyChanged(object sender, string propertyName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null) handler(sender, new PropertyChangedEventArgs(propertyName));
    }

#if !DLL
    public virtual void Read(ISerializerStream stream)
    {
        Identifier = stream.DeserializeString("Identifier");
    }
    public virtual void Write(ISerializerStream stream)
    {
        stream.SerializeString("Identifier", Identifier);
    }

    [Obsolete]
    protected ICommand Command(Action command)
    {
        return new Command(command);
    }

    [Obsolete]
    protected ICommand Command(Func<IEnumerator> command)
    {
        return new YieldCommand(command);
    }

#endif

    public IDisposable Subscribe(IObserver<IObservableProperty> observer)
    {
        PropertyChangedEventHandler propertyChanged = (sender, args) =>
        {
            var property = sender as IObservableProperty;
            //if (property != null)
                observer.OnNext(property);
        };

        PropertyChanged += propertyChanged;
        return new SimpleDisposable(() => PropertyChanged -= propertyChanged);
    }

    public override string ToString()
    {
        // TODO
        return base.ToString();
    }

    public void Dispose()
    {
        if (Aggregator != null)
        Aggregator.Publish(new ViewModelDestroyedEvent()
        {
            ViewModel = this
        });

        Unbind();
    }

    public virtual void Unbind()
    {
        foreach (var binding in Bindings)
        {
            foreach (var binding1 in binding.Value)
            {
                binding1.Dispose();
            }
            binding.Value.Clear();
        }
        Bindings.Clear();
    }


    protected virtual void FillCommands(List<ViewModelCommandInfo> list)
    {
    }

    protected virtual void FillProperties(List<ViewModelPropertyInfo> list)
    {
    }

    protected virtual void WireCommands(Controller controller)
    {
    }

    
    private void CacheReflectedModelProperties()
    {
        if (_modelProperties != null) return;
        _modelProperties = GetViewModelProperties();
    }
}

public class ViewModelCommandInfo
{
    public ISignal Signal { get; private set; }
    [Obsolete]
    public ICommand Command { get; set; }

    public string Name { get; set; }

    public Type ParameterType { get; set; }

    [Obsolete]
    public ViewModelCommandInfo(string name, ICommand command)
    {
        Name = name;
        Command = command;
    }

    public ViewModelCommandInfo(string name, ISignal signal)
    {
        Signal = signal;
        Name = name;
    }
    [Obsolete]
    public ViewModelCommandInfo(Type parameterType, string name, ICommand command)
    {
        ParameterType = parameterType;
        Name = name;
        Command = command;
    }
}

public interface ISignal
{
    Type SignalType { get; }
    void Publish(object data);
    void Publish();
}
public class Signal<TClass> : ISubject<TClass>, ISignal where TClass : ViewModelCommand, new()
{
    private readonly IEventAggregator _aggregator;
    private readonly ViewModel _viewModel;

    public Signal(ViewModel viewModel, IEventAggregator aggregator)
    {
        _aggregator = aggregator;
        _viewModel = viewModel;
    }

    public void OnCompleted()
    {

    }

    public void OnError(Exception error)
    {

    }

    public void OnNext(TClass value)
    {
        value.Sender = _viewModel;
        _aggregator.Publish(value);
    }

    public IDisposable Subscribe(IObserver<TClass> observer)
    {
        return _aggregator.GetEvent<TClass>().Subscribe(observer);
    }

    public Type SignalType
    {
        get { return typeof (TClass); }
    }

    public void Publish(object data)
    {
        OnNext(data as TClass);
    }

    public void Publish()
    {
        OnNext(new TClass()
        {
            Sender = _viewModel
        });
    }
}
public class ViewModelPropertyInfo
{
    public bool IsComputed { get; set; }
    public bool IsCollectionProperty { get; set; }

    public bool IsElementProperty { get; set; }

    public bool IsEnum { get; set; }

    public IObservableProperty Property { get; set; }

    public ViewModelPropertyInfo(IObservableProperty property, bool isElementProperty, bool isCollectionProperty, bool isEnum, bool isComputed = false)
    {
        Property = property;
        IsElementProperty = isElementProperty;
        IsCollectionProperty = isCollectionProperty;
        IsEnum = isEnum;
        IsComputed = isComputed;
    }
}

#if DLL
}
#endif