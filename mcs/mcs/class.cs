//
// class.cs: Class and Struct handlers
//
// Author: Miguel de Icaza (miguel@gnu.org)
//
// Licensed under the terms of the GNU GPL
//
// (C) 2001 Ximian, Inc (http://www.ximian.com)
//
//

using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System;

namespace Mono.CSharp {

	/// <summary>
	///   This is the base class for structs and classes.  
	/// </summary>
	public class TypeContainer : DeclSpace {
		protected int mod_flags;

		// Holds a list of classes and structures
		ArrayList types;

		// Holds the list of properties
		ArrayList properties;

		// Holds the list of enumerations
		ArrayList enums;

		// Holds the list of delegates
		ArrayList delegates;
		
		// Holds the list of constructors
		ArrayList constructors;

		// Holds the list of fields
		ArrayList fields;

		// Holds a list of fields that have initializers
		ArrayList initialized_fields;

		// Holds a list of static fields that have initializers
		ArrayList initialized_static_fields;

		// Holds the list of constants
		ArrayList constants;

		// Holds the list of
		ArrayList interfaces;

		// Holds the methods.
		ArrayList methods;

		// Holds the events
		ArrayList events;

		// Holds the indexers
		ArrayList indexers;

		// Holds the operators
		ArrayList operators;

		//
		// Pointers to the default constructor and the default static constructor
		//
		Constructor default_constructor;
		Constructor default_static_constructor;

		//
		// Whether we have seen a static constructor for this class or not
		//
		bool have_static_constructor = false;
		
		//
		// This is the namespace in which this typecontainer
		// was declared.  We use this to resolve names.
		//
		Namespace my_namespace;
		
		//
		// This one is computed after we can distinguish interfaces
		// from classes from the arraylist `type_bases' 
		//
		string     base_class_name;

		TypeContainer parent;
		ArrayList type_bases;

		//
		// This behaves like a property ;-)
		//
		public readonly RootContext RootContext;

		// Attributes for this type
		protected Attributes attributes;

		// Information in the case we are an attribute type

		public AttributeTargets Targets;
		public bool AllowMultiple;
		public bool Inherited;
		

		public TypeContainer (RootContext rc, TypeContainer parent, string name, Location l)
			: base (name, l)
		{
			string n;
			types = new ArrayList ();
			this.parent = parent;
			RootContext = rc;

			if (parent == null)
				n = "";
			else
				n = parent.Name;

			base_class_name = null;
			
			//Console.WriteLine ("New class " + name + " inside " + n);
		}

		public AdditionResult AddConstant (Const constant)
		{
			AdditionResult res;
			string name = constant.Name;

			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;
			
			if (constants == null)
				constants = new ArrayList ();

			constants.Add (constant);
			DefineName (name, constant);

			return AdditionResult.Success;
		}

		public AdditionResult AddEnum (Mono.CSharp.Enum e)
		{
			AdditionResult res;
			string name = e.Name;

			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			if (enums == null)
				enums = new ArrayList ();

			enums.Add (e);
			DefineName (name, e);

			return AdditionResult.Success;
		}
		
		public AdditionResult AddClass (Class c)
		{
			AdditionResult res;
			string name = c.Name;


			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			DefineName (name, c);
			types.Add (c);

			return AdditionResult.Success;
		}

		public AdditionResult AddStruct (Struct s)
		{
			AdditionResult res;
			string name = s.Name;
			
			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			DefineName (name, s);
			types.Add (s);

			return AdditionResult.Success;
		}

		public AdditionResult AddDelegate (Delegate d)
		{
			AdditionResult res;
			string name = d.Name;

			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			if (delegates == null)
				delegates = new ArrayList ();
			
			DefineName (name, d);
			delegates.Add (d);

			return AdditionResult.Success;
		}

		public AdditionResult AddMethod (Method method)
		{
			string name = method.Name;
			Object value = defined_names [name];
			
			if (value != null && (!(value is Method)))
				return AdditionResult.NameExists;

			if (methods == null)
				methods = new ArrayList ();

			methods.Add (method);
			if (value != null)
				DefineName (name, method);

			return AdditionResult.Success;
		}

		public AdditionResult AddConstructor (Constructor c)
		{
			if (c.Name != Basename) 
				return AdditionResult.NotAConstructor;

			if (constructors == null)
				constructors = new ArrayList ();

			constructors.Add (c);

			bool is_static = (c.ModFlags & Modifiers.STATIC) != 0;
			
			if (is_static)
				have_static_constructor = true;

			if (c.IsDefault ()) {
				if (is_static)
					default_static_constructor = c;
				else
					default_constructor = c;
			}
			
			return AdditionResult.Success;
		}
		
		public AdditionResult AddInterface (Interface iface)
		{
			AdditionResult res;
			string name = iface.Name;

			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;
			
			if (interfaces == null)
				interfaces = new ArrayList ();
			interfaces.Add (iface);
			DefineName (name, iface);
			
			return AdditionResult.Success;
		}

		public AdditionResult AddField (Field field)
		{
			AdditionResult res;
			string name = field.Name;
			
			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			if (fields == null)
				fields = new ArrayList ();

			fields.Add (field);
			if (field.Initializer != null){
				if ((field.ModFlags & Modifiers.STATIC) != 0){
					if (initialized_static_fields == null)
						initialized_static_fields = new ArrayList ();

					initialized_static_fields.Add (field);

					//
					// We have not seen a static constructor,
					// but we will provide static initialization of fields
					//
					have_static_constructor = true;
				} else {
					if (initialized_fields == null)
						initialized_fields = new ArrayList ();
				
					initialized_fields.Add (field);
				}
			}
			
			DefineName (name, field);
			return AdditionResult.Success;
		}

		public AdditionResult AddProperty (Property prop)
		{
			AdditionResult res;
			string name = prop.Name;

			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			if (properties == null)
				properties = new ArrayList ();

			properties.Add (prop);
			DefineName (name, prop);

			return AdditionResult.Success;
		}

		public AdditionResult AddEvent (Event e)
		{
			AdditionResult res;
			string name = e.Name;

			if ((res = IsValid (name)) != AdditionResult.Success)
				return res;

			if (events == null)
				events = new ArrayList ();
			
			events.Add (e);
			DefineName (name, e);

			return AdditionResult.Success;
		}

		public AdditionResult AddIndexer (Indexer i)
		{
			if (indexers == null)
				indexers = new ArrayList ();

			indexers.Add (i);

			return AdditionResult.Success;
		}

		public AdditionResult AddOperator (Operator op)
		{
			if (operators == null)
				operators = new ArrayList ();

			operators.Add (op);

			return AdditionResult.Success;
		}
		
		public TypeContainer Parent {
			get {
				return parent;
			}
		}

		public ArrayList Types {
			get {
				return types;
			}
		}

		public ArrayList Methods {
			get {
				return methods;
			}
		}

		public ArrayList Constants {
			get {
				return constants;
			}
		}

		public ArrayList Interfaces {
			get {
				return interfaces;
			}
		}
		
		public int ModFlags {
			get {
				return mod_flags;
			}
		}

		public string Base {
			get {
				return base_class_name;
			}
		}
		
		public ArrayList Bases {
			get {
				return type_bases;
			}

			set {
				type_bases = value;
			}
		}

		public ArrayList Fields {
			get {
				return fields;
			}
		}

		public ArrayList Constructors {
			get {
				return constructors;
			}
		}

		public ArrayList Properties {
			get {
				return properties;
			}
		}

		public ArrayList Events {
			get {
				return events;
			}
		}
		
		public ArrayList Enums {
			get {
				return enums;
			}
		}

		public ArrayList Indexers {
			get {
				return indexers;
			}
		}

		public ArrayList Operators {
			get {
				return operators;
			}
		}

		public ArrayList Delegates {
			get {
				return delegates;
			}
		}
		
		public Attributes OptAttributes {
			get {
				return attributes;
			}
		}
		
		public Namespace Namespace {
			get {
				return my_namespace;
			}

			set {
				my_namespace = value;
			}
		}

		// 
		// root_types contains all the types.  All TopLevel types
		// hence have a parent that points to `root_types', that is
		// why there is a non-obvious test down here.
		//
		public bool IsTopLevel {
			get {
				if (parent != null){
					if (parent.Parent == null)
						return true;
				}
				return false;
			}
		}
			
		public bool HaveStaticConstructor {
			get {
				return have_static_constructor;
			}
		}
		
		public virtual TypeAttributes TypeAttr {
			get {
				return Modifiers.TypeAttr (mod_flags, this);
			}
		}

		//
		// Emits the instance field initializers
		//
		public bool EmitFieldInitializers (EmitContext ec, bool is_static)
		{
			ArrayList fields;
			ILGenerator ig = ec.ig;
			
			if (is_static)
				fields = initialized_static_fields;
			else
				fields = initialized_fields;

			if (fields == null)
				return true;
			
			foreach (Field f in fields){
				Object init = f.Initializer;

				Expression e;
				if (init is Expression)
					e = (Expression) init;
				else {
					string base_type = f.Type.Substring (0, f.Type.IndexOf ("["));
					string rank = f.Type.Substring (f.Type.IndexOf ("["));
					e = new ArrayCreation (base_type, rank, (ArrayList) init, f.Location); 
				}
				
				e = e.Resolve (ec);
				if (e == null)
					return false;
				
				if (!is_static)
					ig.Emit (OpCodes.Ldarg_0);
				
				e.Emit (ec);
				
				if (is_static)
					ig.Emit (OpCodes.Stsfld, f.FieldBuilder);
				else
					ig.Emit (OpCodes.Stfld, f.FieldBuilder);
				
			}
			
			return true;
		}
		
		//
		// Defines the default constructors
		//
		void DefineDefaultConstructor (bool is_static)
		{
			Constructor c;
			int mods = 0;

			c = new Constructor (Basename, Parameters.GetEmptyReadOnlyParameters (),
					     new ConstructorBaseInitializer (null, new Location (-1)),
					     new Location (-1));
			
			AddConstructor (c);
			
			c.Block = new Block (null);
			
			if (is_static)
				mods = Modifiers.STATIC;

			c.ModFlags = mods;

		}

		public void ReportStructInitializedInstanceError ()
		{
			string n = TypeBuilder.FullName;
			
			foreach (Field f in initialized_fields){
				Report.Error (
					573, Location,
					"`" + n + "." + f.Name + "': can not have " +
					"instance field initializers in structs");
			}
		}

		struct TypeAndMethods {
			public Type          type;
			public MethodInfo [] methods;

			// Far from ideal, but we want to avoid creating a copy
			// of methods above.
			public Type [][]     args;

			//
			// This flag on the method says `We found a match, but
			// because it was private, we could not use the match
			//
			public bool []       found;
		}

		//
		// This array keeps track of the pending implementations
		// 
		TypeAndMethods [] pending_implementations;
		
		//
		// Registers the required method implementations for this class
		//
		// Register method implementations are either abstract methods
		// flagged as such on the base class or interface methods
		//
		public void RegisterRequiredImplementations ()
		{
			Type [] ifaces = TypeBuilder.GetInterfaces ();
			Type b = TypeBuilder.BaseType;
			int icount = 0;
			
			if (ifaces != null)
				icount = ifaces.Length;

			if (icount == 0)
				return;
			
			pending_implementations = new TypeAndMethods [icount + (b.IsAbstract ? 1 : 0)];
			
			int i = 0;
			if (ifaces != null){
				foreach (Type t in ifaces){
					MethodInfo [] mi;

					if (t is TypeBuilder){
						Interface iface;

						iface = RootContext.TypeManager.LookupInterface (t);
						
						mi = iface.GetMethods ();
					} else
						mi = t.GetMethods ();

					int count = mi.Length;
					pending_implementations [i].type = t;
					pending_implementations [i].methods = mi;
					pending_implementations [i].args = new Type [count][];
					pending_implementations [i].found = new bool [count];
					
					int j = 0;
					foreach (MethodInfo m in mi){
						Type [] types = TypeManager.GetArgumentTypes (m);
						pending_implementations [i].args [j] = types;
						j++;
					}
					i++;
				}
			}

			if (b.IsAbstract){
				MemberInfo [] abstract_methods;

				abstract_methods = FindMembers (
					TypeBuilder.BaseType,
					MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance,
					abstract_method_filter, null);

				if (abstract_methods != null){
					pending_implementations [i].methods = new
						MethodInfo [abstract_methods.Length];
					
					abstract_methods.CopyTo (pending_implementations [i].methods, 0);
					pending_implementations [i].type = TypeBuilder;
				}
			}
			
		}

		public static string MakeFQN (string nsn, string name)
		{
			string prefix = (nsn == "" ? "" : nsn + ".");

			return prefix + name;
		}
		       
		Type LookupInterfaceOrClass (object builder, string ns, string name, bool is_class, out bool error)
		{
			TypeContainer parent;
			Type t;

			error = false;
			name = MakeFQN (ns, name);

			t  = RootContext.TypeManager.LookupType (name);
			if (t != null)
				return t;

			if (is_class)
				parent = (Class) RootContext.Tree.Classes [name];
			else 
				parent = (Struct) RootContext.Tree.Structs [name];
			

			if (parent != null){
				t = parent.DefineType (builder);
				if (t == null){
					Report.Error (146, "Class definition is circular: `"+name+"'");
					error = true;
					return null;
				}

				return t;
			}

			return null;
		}
		
		//
		// returns the type for an interface or a class, this will recursively
		// try to define the types that it depends on.
		//
		Type GetInterfaceOrClass (object builder, string name, bool is_class)
		{
			Type t;
			bool error;

			//
			// Attempt to lookup the class on our namespace
			//
			t = LookupInterfaceOrClass (builder, Namespace.Name, name, is_class, out error);
			if (error)
				return null;
			
			if (t != null) 
				return t;

			//
			// Attempt to do a direct unqualified lookup
			//
			t = LookupInterfaceOrClass (builder, "", name, is_class, out error);
			if (error)
				return null;
			
			if (t != null)
				return t;
			
			//
			// Attempt to lookup the class on any of the `using'
			// namespaces
			//

			for (Namespace ns = Namespace; ns != null; ns = ns.Parent){
				ArrayList using_list = ns.UsingTable;
				
				if (using_list == null)
					continue;

				foreach (string n in using_list){
					t = LookupInterfaceOrClass (builder, n, name, is_class, out error);
					if (error)
						return null;

					if (t != null)
						return t;
				}
				
			}
			Report.Error (246, "Can not find type `"+name+"'");
			return null;
		}

		/// <summary>
		///   This function computes the Base class and also the
		///   list of interfaces that the class or struct @c implements.
		///   
		///   The return value is an array (might be null) of
		///   interfaces implemented (as Types).
		///   
		///   The @parent argument is set to the parent object or null
		///   if this is `System.Object'. 
		/// </summary>
		Type [] GetClassBases (object builder, bool is_class, out Type parent, out bool error)
		{
			ArrayList bases = Bases;
			int count;
			int start, j, i;

			error = false;

			if (is_class)
				parent = null;
			else
				parent = TypeManager.value_type;

			if (bases == null){
				if (is_class){
					if (RootContext.StdLib)
						parent = TypeManager.object_type;
					else if (Name != "System.Object")
						parent = TypeManager.object_type;
				} else {
					//
					// If we are compiling our runtime,
					// and we are defining ValueType, then our
					// parent is `System.Object'.
					//
					if (!RootContext.StdLib && Name == "System.ValueType")
						parent = TypeManager.object_type;
				}

				return null;
			}

			//
			// Bases should be null if there are no bases at all
			//
			count = bases.Count;

			if (is_class){
				string name = (string) bases [0];
				Type first = GetInterfaceOrClass (builder, name, is_class);

				if (first == null){
					error = true;
					return null;
				}
				
				if (first.IsClass){
					parent = first;
					start = 1;
				} else {
					parent = TypeManager.object_type;
					start = 0;
				}
			} else {
				start = 0;
			}

			Type [] ifaces = new Type [count-start];
			
			for (i = start, j = 0; i < count; i++, j++){
				string name = (string) bases [i];
				Type t = GetInterfaceOrClass (builder, name, is_class);
				
				if (t == null){
					error = true;
					return null;
				}

				if (is_class == false && !t.IsInterface){
					Report.Error (527, "In Struct `" + Name + "', type `"+
						      name +"' is not an interface");
					error = true;
					return null;
				}
				
				if (t.IsSealed) {
					string detail = "";
					
					if (t.IsValueType)
						detail = " (a class can not inherit from a struct)";
							
					Report.Error (509, "class `"+ Name +
						      "': Cannot inherit from sealed class `"+
						      bases [i]+"'"+detail);
					error = true;
					return null;
				}

				if (t.IsClass) {
					if (parent != null){
						Report.Error (527, "In Class `" + Name + "', type `"+
							      name+"' is not an interface");
						error = true;
						return null;
					}
				}
				
				ifaces [j] = t;
			}

			return ifaces;
		}
		
		//
		// Defines the type in the appropriate ModuleBuilder or TypeBuilder.
		//
		public TypeBuilder DefineType (object parent_builder)
		{
			Type parent;
			Type [] ifaces;
			bool error;
			bool is_class;
			
			if (InTransit)
				return null;
			
			InTransit = true;
			
			if (this is Class)
				is_class = true;
			else
				is_class = false;
			
			ifaces = GetClassBases (parent_builder, is_class, out parent, out error); 
			
			if (error)
				return null;
			
			if (parent_builder is ModuleBuilder) {
				ModuleBuilder builder = (ModuleBuilder) parent_builder;

				//
				// Structs with no fields need to have a ".size 1"
				// appended
				//
				if (!is_class && Fields == null)
					TypeBuilder = builder.DefineType (Name,
									  TypeAttr,
									  parent, 
									  PackingSize.Unspecified, 1);
				else
				//
				// classes or structs with fields
				//
					TypeBuilder = builder.DefineType (Name,
									  TypeAttr,
									  parent,
									  ifaces);
			} else {
				TypeBuilder builder = (TypeBuilder) parent_builder;
				
				//
				// Structs with no fields need to have a ".size 1"
				// appended
				//
				if (!is_class && Fields == null)
					TypeBuilder = builder.DefineNestedType (Name,
										TypeAttr,
										parent, 
										PackingSize.Unspecified);
				else
				//
				// classes or structs with fields
				//
					TypeBuilder = builder.DefineNestedType (Name,
										TypeAttr,
										parent,
										ifaces);
			}

			RootContext.TypeManager.AddUserType (Name, TypeBuilder, this);

			if (Types != null) {
				foreach (TypeContainer tc in Types)
					tc.DefineType (TypeBuilder);
			}

			if (Delegates != null) {
				foreach (Delegate d in Delegates)
					d.DefineDelegate (TypeBuilder);
			}

			if (Enums != null) {
				foreach (Enum en in Enums)
					en.DefineEnum (TypeBuilder);
			}
			
			InTransit = false;
			return TypeBuilder;
		}
		
		/// <summary>
		///   Populates our TypeBuilder with fields and methods
		/// </summary>
		public void Populate ()
		{
			if (Constants != null){
				foreach (Const c in Constants)
					c.Define (this);
			}

			if (Fields != null){
				foreach (Field f in Fields)
					f.Define (this);
			} 

			if (this is Class && constructors == null){
				if (default_constructor == null) 
					DefineDefaultConstructor (false);

				if (initialized_static_fields != null &&
				    default_static_constructor == null)
					DefineDefaultConstructor (true);
			}

			if (this is Struct){
				//
				// Structs can not have initialized instance
				// fields
				//
				if (initialized_static_fields != null &&
				    default_static_constructor == null)
					DefineDefaultConstructor (true);

				if (initialized_fields != null)
					ReportStructInitializedInstanceError ();
			}

			RegisterRequiredImplementations ();
			
			ArrayList remove_list = new ArrayList ();

			if (constructors != null){
				foreach (Constructor c in constructors){
					MethodBase builder = c.Define (this);
					
					if (builder == null)
						remove_list.Add (c);
				}

				foreach (object o in remove_list)
					constructors.Remove (o);
				
				remove_list.Clear ();
			} 

			if (Methods != null){
				foreach (Method m in methods){
					MethodBase key = m.Define (this);

					//
					// FIXME:
					// The following key is not enoug
					// class x { public void X ()  {} }
					// class y : x { public void X () {}}
					// fails
					
					if (key == null)
						remove_list.Add (m);
				}
				foreach (object o in remove_list)
					methods.Remove (o);
				
				remove_list.Clear ();
			}

			if (Properties != null) {
				foreach (Property p in Properties)
					p.Define (this);
			}

			if (Events != null) {
				foreach (Event e in Events)
					e.Define (this);
			}

			if (Indexers != null) {
				foreach (Indexer i in Indexers)
					i.Define (this);
			}

			if (Operators != null) {
				foreach (Operator o in Operators) 
					o.Define (this);
			}

			if (Enums != null)
				foreach (Enum en in Enums)
					en.Populate (this);
			
			if (Delegates != null) {
				foreach (Delegate d in Delegates) 
					d.Populate (this);
			}
			
			if (Types != null) {
				foreach (TypeContainer tc in Types)
					tc.Populate ();
			}

		
		}

		public Type LookupType (string name, bool silent)
		{
			return RootContext.LookupType (this, name, silent);
		}

		/// <summary>
		///   Looks up the alias for the name
		/// </summary>
		public string LookupAlias (string name)
		{
			if (my_namespace != null)
				return my_namespace.LookupAlias (name);
			else
				return null;
		}
		
		/// <summary>
		///   This function is based by a delegate to the FindMembers routine
		/// </summary>
		static bool AlwaysAccept (MemberInfo m, object filterCriteria)
		{
			return true;
		}
		
		static bool IsAbstractMethod (MemberInfo m, object filter_criteria)
		{
			MethodInfo mi = (MethodInfo) m;

			return mi.IsAbstract;
		}

		/// <summary>
		///   This filter is used by FindMembers, and we just keep
		///   a global for the filter to `AlwaysAccept'
		/// </summary>
		static MemberFilter accepting_filter;
		
		/// <summary>
		///    This delegate is a MemberFilter used to extract the 
		///    abstact methods from a type.  
		/// </summary>
		static MemberFilter abstract_method_filter;

		static TypeContainer ()
		{
			abstract_method_filter = new MemberFilter (IsAbstractMethod);
			accepting_filter = new MemberFilter (AlwaysAccept);
		}
		
		/// <summary>
		///   This method returns the members of this type just like Type.FindMembers would
		///   Only, we need to use this for types which are _being_ defined because MS' 
		///   implementation can't take care of that.
		/// </summary>
		public MemberInfo [] FindMembers (MemberTypes mt, BindingFlags bf,
						  MemberFilter filter, object criteria)
		{
			ArrayList members = new ArrayList ();

			if (filter == null)
				filter = accepting_filter; 
			
			if ((mt & MemberTypes.Field) != 0) {
				if (Fields != null) {
					foreach (Field f in Fields) {
						FieldBuilder fb = f.FieldBuilder;
						if (filter (fb, criteria) == true)
							members.Add (fb);
					}
				}

				if (Constants != null) {
					foreach (Const con in Constants) {
						FieldBuilder fb = con.FieldBuilder;
						if (filter (fb, criteria) == true)
							members.Add (fb);
					}
				}
			}

			if ((mt & MemberTypes.Method) != 0) {
				if (Methods != null){
					foreach (Method m in Methods) {
						MethodBuilder mb = m.MethodBuilder;

						if (filter (mb, criteria) == true)
							members.Add (mb);
					}
				}

				if (Operators != null){
					foreach (Operator o in Operators) {
						MethodBuilder ob = o.OperatorMethodBuilder;

						if (filter (ob, criteria) == true)
							members.Add (ob);
					}
				}
			}

			// FIXME : This ain't right because EventBuilder is not a
			// MemberInfo. What do we do ?
			
			if ((mt & MemberTypes.Event) != 0 && Events != null) {
				//foreach (Event e in Events) {
				//	if (filter (e.EventBuilder, criteria) == true)
				//		mi [i++] = e.EventBuilder;
				//}
			}

			if ((mt & MemberTypes.Property) != 0){
				if (Properties != null)
					foreach (Property p in Properties) {
						if (filter (p.PropertyBuilder, criteria) == true)
							members.Add (p.PropertyBuilder);
					}

				if (Indexers != null)
					foreach (Indexer ix in Indexers) {
						if (filter (ix.PropertyBuilder, criteria) == true)
							members.Add (ix.PropertyBuilder);
					}
			}
			
			if ((mt & MemberTypes.NestedType) != 0) {

				if (Types != null)
					foreach (TypeContainer t in Types)  
						if (filter (t.TypeBuilder, criteria) == true)
							members.Add (t.TypeBuilder);

				if (Enums != null)
					foreach (Enum en in Enums)
						if (filter (en.EnumBuilder, criteria) == true)
							members.Add (en.EnumBuilder);
				
			}

			if ((mt & MemberTypes.Constructor) != 0){
				if (Constructors != null){
					foreach (Constructor c in Constructors){
						ConstructorBuilder cb = c.ConstructorBuilder;

						if (filter (cb, criteria) == true)
							members.Add (cb);
					}
				}
			}

			//
			// Lookup members in parent if requested.
			//
			if ((bf & BindingFlags.DeclaredOnly) == 0){
				MemberInfo [] mi;

				mi = FindMembers (TypeBuilder.BaseType, mt, bf, filter, criteria);
				if (mi != null)
					members.AddRange (mi);
			}
			
			int count = members.Count;
			if (count > 0){
				MemberInfo [] mi = new MemberInfo [count];
				members.CopyTo (mi);
				return mi;
			}

			return null;
		}

		public static MemberInfo [] FindMembers (Type t, MemberTypes mt, BindingFlags bf,
							 MemberFilter filter, object criteria)
		{
			TypeContainer tc = TypeManager.LookupTypeContainer (t);

			if (tc != null)
				return tc.FindMembers (mt, bf, filter, criteria);
			else
				return t.FindMembers (mt, bf, filter, criteria);
		}
		
		/// <summary>
		///   Whether the specified method is an interface method implementation
		/// </summary>
		///
		/// <remarks>
		///   If a method in Type `t' (or null to look in all interfaces
		///   and the base abstract class) with name `Name', return type `ret_type' and
		///   arguments `args' implements an interface, this method will
		///   return the MethodInfo that this method implements.
		///
		///   This will remove the method from the list of "pending" methods
		///   that are required to be implemented for this class as a side effect.
		/// 
		/// </remarks>
		public MethodInfo IsInterfaceMethod (Type t, string Name, Type ret_type, Type [] args,
						     bool clear)
		{
			if (pending_implementations == null)
				return null;
			
			foreach (TypeAndMethods tm in pending_implementations){
				if (!(t == null || tm.type == t))
					continue;

				int i = 0;
				foreach (MethodInfo m in tm.methods){
					if (m == null){
						i++;
						continue;
					}
					
					if (Name != m.Name){
						i++;
						continue;
					}
					
					if (ret_type != m.ReturnType){
						i++;
						continue;
					}
					
					if (args == null){
						if (tm.args [i] == null || tm.args [i].Length == 0){
							if (clear)
								tm.methods [i] = null;
							tm.found [i] = true;
							return m;
						} 
						i++;
						continue;
					}
					
					if (tm.args [i] == null){
						i++;
						continue;
					}

					//
					// Check if we have the same parameters
					//
					if (tm.args [i].Length != args.Length){
						i++;
						continue;
					}
					
					int j;
						
					for (j = 0; j < args.Length; j++){
						if (tm.args [i][j] != args[i]){
							i++;
							continue;
						}
					}

					if (clear)
						tm.methods [i] = null;
					tm.found [i] = true;
					return m;
				}

				// If a specific type was requested, we can stop now.
				if (tm.type == t)
					return null;
			}
			return null;
		}

		/// <summary>
		///   Verifies that any pending abstract methods or interface methods
		///   were implemented.
		/// </summary>
		bool VerifyPendingMethods ()
		{
			int top = pending_implementations.Length;
			bool errors = false;
			int i;
			
			for (i = 0; i < top; i++){
				Type type = pending_implementations [i].type;
				int j = 0;
				
				foreach (MethodInfo mi in pending_implementations [i].methods){
					if (mi == null)
						continue;

					if (type.IsInterface){
						string extra = "";
						
						if (pending_implementations [i].found [j])
							extra = ".  (method might be private or static)";
						Report.Error (
							536, Location,
							"`" + Name + "' does not implement " +
							"interface member `" +
							type.FullName + "." + mi.Name + "'" + extra);
					} else {
						Report.Error (
							534, Location,
							"`" + Name + "' does not implement " +
							"inherited abstract member `" +
							type.FullName + "." + mi.Name + "'");
					}
					errors = true;
					j++;
				}
			}
			return errors;
		}
		
		/// <summary>
		///   Emits the code, this step is performed after all
		///   the types, enumerations, constructors
		/// </summary>
		public void Emit ()
		{
			if (constants != null)
				foreach (Const con in constants)
					con.EmitConstant (this);
			
			if (constructors != null)
				foreach (Constructor c in constructors)
					c.Emit (this);
			
			if (methods != null)
				foreach (Method m in methods)
					m.Emit (this);

			if (operators != null)
				foreach (Operator o in operators)
					o.Emit (this);

			if (properties != null)
				foreach (Property p in properties)
					p.Emit (this);

			if (indexers != null)
				foreach (Indexer ix in indexers)
					ix.Emit (this);

			if (fields != null)
				foreach (Field f in fields)
					f.Emit (this);

			if (events != null)
				foreach (Event e in Events)
					e.Emit (this);
			
			if (pending_implementations != null)
				if (!VerifyPendingMethods ())
					return;

			if (OptAttributes != null) {
				EmitContext ec = new EmitContext (this, Location.Null, null, null,
								  ModFlags, false);
				
				if (OptAttributes.AttributeSections != null) {
					foreach (AttributeSection asec in OptAttributes.AttributeSections) {
						if (asec.Attributes == null)
							continue;
						
						foreach (Attribute a in asec.Attributes) {
							CustomAttributeBuilder cb = a.Resolve (ec);
							if (cb == null)
								continue;
							
							if (a.UsageAttr) {
								this.Targets = a.Targets;
								this.AllowMultiple = a.AllowMultiple;
								this.Inherited = a.Inherited;
								
								RootContext.TypeManager.RegisterAttrType (
									TypeBuilder, this);
							} else {
								
								if (!Attribute.CheckAttribute (a, this)) {
									Attribute.Error592 (a, Location);
									return;
								}
							}
							
							TypeBuilder.SetCustomAttribute (cb);
						}
					}
				}
			}
			
			if (types != null)
				foreach (TypeContainer tc in types)
					tc.Emit ();
		}
		
		public void CloseType ()
		{
			try {
				TypeBuilder.CreateType ();
			} catch (InvalidOperationException e){
				Console.WriteLine ("Exception while creating class: " + TypeBuilder.Name);
				Console.WriteLine ("Message:" + e.Message);
			}
			
			if (Types != null)
				foreach (TypeContainer tc in Types)
					tc.CloseType ();

			if (Enums != null)
				foreach (Enum en in Enums)
					en.CloseEnum ();
			
			if (Delegates != null)
				foreach (Delegate d in Delegates)
					d.CloseDelegate ();
		}

		public string MakeName (string n)
		{
			return "`" + Name + "." + n + "'";
		}
		
		public static int CheckMember (string name, MemberInfo mi, int ModFlags)
		{
			return 0;
		}

		//
		// Performs the validation on a Method's modifiers (properties have
		// the same properties).
		//
		public bool MethodModifiersValid (int flags, string n, Location loc)
		{
			const int vao = (Modifiers.VIRTUAL | Modifiers.ABSTRACT | Modifiers.OVERRIDE);
			const int nv = (Modifiers.NEW | Modifiers.VIRTUAL);
			bool ok = true;
			string name = MakeName (n);
			
			//
			// At most one of static, virtual or override
			//
			if ((flags & Modifiers.STATIC) != 0){
				if ((flags & vao) != 0){
					Report.Error (
						112, loc, "static method " + name + "can not be marked " +
						"as virtual, abstract or override");
					ok = false;
				}
			}

			if ((flags & Modifiers.OVERRIDE) != 0 && (flags & nv) != 0){
				Report.Error (
					113, loc, name +
					" marked as override cannot be marked as new or virtual");
				ok = false;
			}

			//
			// If the declaration includes the abstract modifier, then the
			// declaration does not include static, virtual or extern
			//
			if ((flags & Modifiers.ABSTRACT) != 0){
				if ((flags & Modifiers.EXTERN) != 0){
					Report.Error (
						180, loc, name + " can not be both abstract and extern");
					ok = false;
				}

				if ((flags & Modifiers.VIRTUAL) != 0){
					Report.Error (
						503, loc, name + " can not be both abstract and virtual");
					ok = false;
				}

				if ((ModFlags & Modifiers.ABSTRACT) == 0){
					Report.Error (
						513, loc, name +
						" is abstract but its container class is not");
					ok = false;

				}
			}

			if ((flags & Modifiers.PRIVATE) != 0){
				if ((flags & vao) != 0){
					Report.Error (
						621, loc, name +
						" virtual or abstract members can not be private");
					ok = false;
				}
			}

			if ((flags & Modifiers.SEALED) != 0){
				if ((flags & Modifiers.OVERRIDE) == 0){
					Report.Error (
						238, loc, name +
						" cannot be sealed because it is not an override");
					ok = false;
				}
			}

			return ok;
		}

		//
		// Returns true if `type' is as accessible as the flags `flags'
		// given for this member
		//
		static public bool AsAccessible (Type type, int flags)
		{
			// FIXME: Implement me
			return true;
		}
	}

	public class Class : TypeContainer {
		// <summary>
		//   Modifiers allowed in a class declaration
		// </summary>
		public const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE |
			Modifiers.ABSTRACT |
			Modifiers.SEALED;

		public Class (RootContext rc, TypeContainer parent, string name, int mod,
			      Attributes attrs, Location l)
			: base (rc, parent, name, l)
		{
			int accmods;

			if (parent.Parent == null)
				accmods = Modifiers.INTERNAL;
			else
				accmods = Modifiers.PRIVATE;
			
			this.mod_flags = Modifiers.Check (AllowedModifiers, mod, accmods);
			this.attributes = attrs;
		}

		//
		// FIXME: How do we deal with the user specifying a different
		// layout?
		//
		public override TypeAttributes TypeAttr {
			get {
				return base.TypeAttr | TypeAttributes.AutoLayout | TypeAttributes.Class;
			}
		}
	}

	public class Struct : TypeContainer {
		// <summary>
		//   Modifiers allowed in a struct declaration
		// </summary>
		public const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE;

		public Struct (RootContext rc, TypeContainer parent, string name, int mod,
			       Attributes attrs, Location l)
			: base (rc, parent, name, l)
		{
			int accmods;
			
			if (parent.Parent == null)
				accmods = Modifiers.INTERNAL;
			else
				accmods = Modifiers.PRIVATE;
			
			this.mod_flags = Modifiers.Check (AllowedModifiers, mod, accmods);

			this.mod_flags |= Modifiers.SEALED;
			this.attributes = attrs;
			
		}

		//
		// FIXME: Allow the user to specify a different set of attributes
		// in some cases (Sealed for example is mandatory for a class,
		// but what SequentialLayout can be changed
		//
		public override TypeAttributes TypeAttr {
			get {
				return base.TypeAttr |
					TypeAttributes.SequentialLayout |
					TypeAttributes.Sealed |
					TypeAttributes.BeforeFieldInit;
			}
		}
	}

	public class MemberCore {
		public string Name;
		public int ModFlags;
		public readonly Location Location;

		public MemberCore (string name, Location loc)
		{
			Name = name;
			Location = loc;
		}

		protected void WarningNotHiding (TypeContainer parent)
		{
			Report.Warning (
				109, Location,
				"The member `" + parent.Name + "." + Name + "' does not hide an " +
				"inherited member.  The keyword new is not required");
							   
		}

		static string MethodBaseName (MethodBase mb)
		{
			return "`" + mb.ReflectedType.Name + "." + mb.Name + "'";
		}

		//
		// Performs various checks on the MethodInfo `mb' regarding the modifier flags
		// that have been defined.
		//
		// `name' is the user visible name for reporting errors (this is used to
		// provide the right name regarding method names and properties)
		//
		protected bool CheckMethodAgainstBase (TypeContainer parent, MethodInfo mb)
		{
			bool ok = true;
			
			if ((ModFlags & (Modifiers.NEW | Modifiers.OVERRIDE)) == 0){
				Report.Warning (
					108, Location, "The keyword new is required on " + 
					parent.MakeName (Name) + " because it hides `" +
					mb.ReflectedType.Name + "." +
					mb.Name + "'");
			}

			if ((ModFlags & Modifiers.OVERRIDE) != 0){
				if (!(mb.IsAbstract || mb.IsVirtual)){
					Report.Error (
						506, Location, parent.MakeName (Name) +
						": cannot override inherited member `" +
						mb.ReflectedType.Name + "' because it is not " +
						"virtual, abstract or override");
					ok = false;
				}
			}

			if (mb.IsVirtual || mb.IsAbstract){
				if ((ModFlags & (Modifiers.NEW | Modifiers.OVERRIDE)) == 0){
					Report.Warning (
						114, Location, parent.MakeName (Name) + 
						" hides inherited member " + MethodBaseName (mb) +
						".  To make the current member override that " +
						"implementation, add the override keyword, " +
						"otherwise use the new keyword");
				}
			}

			return ok;
		}
	}
	
	public class MethodCore : MemberCore {
		public readonly Parameters Parameters;
		Block block;
		
		//
		// Parameters, cached for semantic analysis.
		//
		InternalParameters parameter_info;
		
		public MethodCore (string name, Parameters parameters, Location l)
			: base (name, l)
		{
			Name = name;
			Parameters = parameters;
		}
		
		//
		//  Returns the System.Type array for the parameters of this method
		//
		Type [] parameter_types;
		static Type [] no_types = new Type [0];
		public Type [] ParameterTypes (TypeContainer parent)
		{
			if (Parameters == null)
				return no_types;
			
			if (parameter_types == null)
				parameter_types = Parameters.GetParameterInfo (parent);

			return parameter_types;
		}

		public InternalParameters ParameterInfo
		{
			get {
				return parameter_info;
			}

			set {
				parameter_info = value;
			}
		}
		
		public Block Block {
			get {
				return block;
			}

			set {
				block = value;
			}
		}

		public CallingConventions GetCallingConvention (bool is_class)
		{
			CallingConventions cc = 0;
			
			cc = Parameters.GetCallingConvention ();

			if (is_class)
				if ((ModFlags & Modifiers.STATIC) == 0)
					cc |= CallingConventions.HasThis;

			// FIXME: How is `ExplicitThis' used in C#?
			
			return cc;
		}
	}
	
	public class Method : MethodCore {
		public readonly string ReturnType;
		public MethodBuilder MethodBuilder;
		public readonly Attributes OptAttributes;

		MethodAttributes flags;

		/// <summary>
		///   Modifiers allowed in a class declaration
		/// </summary>
		const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE |
			Modifiers.STATIC |
			Modifiers.VIRTUAL |
			Modifiers.SEALED |
			Modifiers.OVERRIDE |
			Modifiers.ABSTRACT |
			Modifiers.EXTERN;

		//
		// return_type can be "null" for VOID values.
		//
		public Method (string return_type, int mod, string name, Parameters parameters,
			       Attributes attrs, Location l)
			: base (name, parameters, l)
		{
			ReturnType = return_type;
			ModFlags = Modifiers.Check (AllowedModifiers, mod, Modifiers.PRIVATE);
			OptAttributes = attrs;
		}

		static bool MemberSignatureCompare (MemberInfo m, object filter_criteria)
		{
			MethodInfo mi;
			
			if (! (m is MethodInfo))
				return false;

			MethodSignature sig = (MethodSignature) filter_criteria;

			if (m.Name != sig.Name)
				return false;
			
			mi = (MethodInfo) m;

			if (mi.ReturnType != sig.RetType)
				return false;

			Type [] args = TypeManager.GetArgumentTypes (mi);
			Type [] sigp = sig.Parameters;

			if (args.Length != sigp.Length)
				return false;

			for (int i = args.Length; i > 0; ){
				i--;
				if (args [i] != sigp [i])
					return false;
			}
			return true;
		}
		
		/// <summary>
		///    This delegate is used to extract methods which have the
		///    same signature as the argument
		/// </summary>
		static MemberFilter method_signature_filter;
		
		static Method ()
		{
			method_signature_filter = new MemberFilter (MemberSignatureCompare);
		}
		
		//
		// Returns the `System.Type' for the ReturnType of this
		// function.  Provides a nice cache.  (used between semantic analysis
		// and actual code generation
		//
		Type type_return_type;
		
		public Type GetReturnType (TypeContainer parent)
		{
			if (type_return_type == null)
				type_return_type = parent.LookupType (ReturnType, false);
			
			return type_return_type;
		}

		//
		// Creates the type
		//
		public MethodBuilder Define (TypeContainer parent)
		{
			Type ret_type = GetReturnType (parent);
			Type [] parameters = ParameterTypes (parent);
			bool error = false;
			MethodInfo implementing;
			Type iface_type = null;
			string iface = "", short_name;

			// Check if the return type and arguments were correct
			if (ret_type == null || parameters == null)
				return null;
			
			if (!parent.MethodModifiersValid (ModFlags, Name, Location))
				return null;

			//
			// verify accessibility
			//
			if (!TypeContainer.AsAccessible (ret_type, ModFlags))
				return null;

			foreach (Type partype in parameters)
				if (!TypeContainer.AsAccessible (partype, ModFlags))
					error = true;

			if (error)
				return null;

			//
			// Verify if the parent has a type with the same name, and then
			// check whether we have to create a new slot for it or not.
			//
			Type ptype = parent.TypeBuilder.BaseType;

			// ptype is only null for System.Object while compiling corlib.
			if (ptype != null){
				MethodSignature ms = new MethodSignature (Name, ret_type, parameters);
				MemberInfo [] mi, mi_static, mi_instance;

				mi_static = TypeContainer.FindMembers (
					ptype, MemberTypes.Method,
					BindingFlags.Public | BindingFlags.Static, method_signature_filter,
					ms);

				mi_instance = TypeContainer.FindMembers (
					ptype, MemberTypes.Method,
					BindingFlags.Public | BindingFlags.Instance, method_signature_filter,
					ms);

				if (mi_instance != null && mi_instance.Length > 0){
					mi = mi_instance;
				} else if (mi_static != null && mi_static.Length > 0)
					mi = mi_static;
				else
					mi = null;
				
				if (mi != null && mi.Length > 0){
					if (!CheckMethodAgainstBase (parent, (MethodInfo) mi [0])){
						return null;
					}
				} else {
					if ((ModFlags & Modifiers.NEW) != 0)
						WarningNotHiding (parent);
					
					if ((ModFlags & Modifiers.OVERRIDE) != 0)
						Report.Error (115, Location,
							      parent.MakeName (Name) +
							      " no suitable methods found to override");
				}
			} else if ((ModFlags & Modifiers.NEW) != 0)
				WarningNotHiding (parent);

			//
			// If we implement an interface, extract the interface name.
			//
			flags = Modifiers.MethodAttr (ModFlags);

			if (Name.IndexOf (".") != -1){
				int pos = Name.LastIndexOf (".");
				iface = Name.Substring (0, pos);

				iface_type = parent.LookupType (iface, false);
				short_name = Name.Substring (pos + 1);

				if (iface_type == null)
					return null;

				Name = iface_type.FullName + "." + short_name;
			} else
				short_name = Name;

			//
			// Check if we are an implementation of an interface method or
			// a method
			//
			implementing = parent.IsInterfaceMethod (
				iface_type, short_name, ret_type, parameters, false);
				
			//
			// For implicit implementations, make sure we are public, for
			// explicit implementations, make sure we are private.
			//
			if (implementing != null){
				if (iface_type == null){
					if ((ModFlags & Modifiers.PUBLIC) == 0){
						//
						// This forces a 536 (not impl) with extra information.
						//
						implementing = null;
					}
					if ((ModFlags & Modifiers.STATIC) != 0){
						//
						// This forces a 536 (not impl) with extra information.
						//
						implementing = null;
					}
					
				} else {
					if ((ModFlags & (Modifiers.PUBLIC | Modifiers.ABSTRACT)) != 0){
						Report.Error (
							106, Location, "`public' or `abstract' modifiers "+
							"are not allowed in explicit interface declarations"
							);
						implementing = null;
					}
				}
			}
			
			//
			// If implementing is still valid, set flags
			//
			if (implementing != null){
				flags |= MethodAttributes.Virtual |
					 MethodAttributes.NewSlot | MethodAttributes.HideBySig;

				// If not abstract, then we can set Final.
				if ((flags & MethodAttributes.Abstract) == 0)
					flags |= MethodAttributes.Final;
				
				parent.IsInterfaceMethod (
					iface_type, short_name, ret_type, parameters, true);
			}

			Attribute dllimport_attr = null;
			if (OptAttributes != null && OptAttributes.AttributeSections != null) {
				foreach (AttributeSection asec in OptAttributes.AttributeSections) {
				 	if (asec.Attributes == null)
						continue;
					
					foreach (Attribute a in asec.Attributes)
						if (a.Name.IndexOf ("DllImport") != -1) {
							flags |= MethodAttributes.PinvokeImpl;
							dllimport_attr = a;
						}
				
				}
			}

			//
			// Finally, define the method
			//

			if ((flags & MethodAttributes.PinvokeImpl) != 0) {
				EmitContext ec = new EmitContext (
					parent, Location, null, GetReturnType (parent), ModFlags);
				
				MethodBuilder = dllimport_attr.DefinePInvokeMethod (
					ec, parent.TypeBuilder,
					Name, flags, ret_type, parameters);
			} else {
				MethodBuilder = parent.TypeBuilder.DefineMethod (
					Name, flags,
					GetCallingConvention (parent is Class),
					ret_type, parameters);

				if (implementing != null){
					parent.TypeBuilder.DefineMethodOverride (
						MethodBuilder, implementing);
				}
			}

			if (MethodBuilder == null)
				return null;

			//
			// HACK because System.Reflection.Emit is lame
			//
			ParameterInfo = new InternalParameters (parent, Parameters);

			if (!TypeManager.RegisterMethod (MethodBuilder, ParameterInfo,
							 parameters)) {
				Report.Error (
					111, Location,
					"Class `" + parent.Name + "' already contains a definition with " +
					" the same return value and parameter types for method `" +
					Name + "'");
				return null;
			}
			
			//
			// This is used to track the Entry Point,
			//
			// FIXME: Allow pluggable entry point, check arguments, etc.
			//
			if (Name == "Main"){
				if ((ModFlags & Modifiers.STATIC) != 0){
					parent.RootContext.EntryPoint = MethodBuilder;

					//
					// FIXME: Verify that the method signature
					// is valid for an entry point, and report
					// error 28 if not.
					//
				}
			}
			
			//
			// Define each type attribute (in/out/ref) and
			// the argument names.
			//
			Parameter [] p = Parameters.FixedParameters;
			if (p != null){
				int i;
				
				for (i = 0; i < p.Length; i++) 
					MethodBuilder.DefineParameter (
						      i + 1, p [i].Attributes, p [i].Name);
					
				if (i != parameters.Length) {
					ParameterBuilder pb;
					
					Parameter array_param = Parameters.ArrayParameter;
					pb = MethodBuilder.DefineParameter (
						i + 1, array_param.Attributes,
						array_param.Name);

					CustomAttributeBuilder a = new CustomAttributeBuilder (
						TypeManager.cons_param_array_attribute, new object [0]);

					pb.SetCustomAttribute (a);
				}
			}

			return MethodBuilder;
		}

		//
		// Emits the code
		// 
		public void Emit (TypeContainer parent)
		{
			if ((flags & MethodAttributes.PinvokeImpl) != 0)
				return;

			ILGenerator ig = MethodBuilder.GetILGenerator ();
			EmitContext ec = new EmitContext (parent, Location, ig,
							  GetReturnType (parent), ModFlags);

			if (OptAttributes != null && OptAttributes.AttributeSections != null) {
				foreach (AttributeSection asec in OptAttributes.AttributeSections) {
				 	if (asec.Attributes == null)
						continue;
					
					foreach (Attribute a in asec.Attributes) {
						CustomAttributeBuilder cb = a.Resolve (ec);
						if (cb == null)
							continue;
						
						if (!Attribute.CheckAttribute (a, this)) {
							Attribute.Error592 (a, Location);
							return;
						}

						if (a.Type != TypeManager.dllimport_type)
							MethodBuilder.SetCustomAttribute (cb);
					}
				}
			}

			//
			// abstract or extern methods have no bodies
			//
			if ((ModFlags & (Modifiers.ABSTRACT | Modifiers.EXTERN)) != 0)
				return;

			//
			// Handle destructors specially
			//
			// FIXME: This code generates buggy code
			//
			if (Name == "Finalize" && type_return_type == TypeManager.void_type){
				Label end = ig.BeginExceptionBlock ();
				Label finish = ig.DefineLabel ();
				
				ec.EmitTopBlock (Block);
				ig.Emit (OpCodes.Leave, finish);
				ig.BeginFinallyBlock ();

//				throw new Exception ("IMPLEMENT BASE ACCESS!");
				Console.WriteLine ("Implement base access here");

				ig.EndExceptionBlock ();
			} else
				ec.EmitTopBlock (Block);
		}
	}

	public abstract class ConstructorInitializer {
		ArrayList argument_list;
		ConstructorInfo parent_constructor;
		Location location;
		
		public ConstructorInitializer (ArrayList argument_list, Location location)
		{
			this.argument_list = argument_list;
			this.location = location;
		}

		public ArrayList Arguments {
			get {
				return argument_list;
			}
		}

		public bool Resolve (EmitContext ec)
		{
			Expression parent_constructor_group;
			
			if (argument_list != null){
				for (int i = argument_list.Count; i > 0; ){
					--i;

					Argument a = (Argument) argument_list [i];
					if (!a.Resolve (ec, location))
						return false;
				}
			}

			parent_constructor_group = Expression.MemberLookup (
				ec,
				ec.TypeContainer.TypeBuilder.BaseType, ".ctor", true,
				MemberTypes.Constructor,
				BindingFlags.Public | BindingFlags.Instance, location);
			
			if (parent_constructor_group == null){
				Console.WriteLine ("Could not find a constructor in our parent");
				return false;
			}
			
			parent_constructor = (ConstructorInfo) Invocation.OverloadResolve (ec, 
				(MethodGroupExpr) parent_constructor_group, argument_list, location);
			
			if (parent_constructor == null)
				return false;
			
			return true;
		}

		public void Emit (EmitContext ec)
		{
			ec.ig.Emit (OpCodes.Ldarg_0);
			if (argument_list != null)
				Invocation.EmitArguments (ec, null, argument_list);
			ec.ig.Emit (OpCodes.Call, parent_constructor);
		}
	}

	public class ConstructorBaseInitializer : ConstructorInitializer {
		public ConstructorBaseInitializer (ArrayList argument_list, Location l) : base (argument_list, l)
		{
		}
	}

	public class ConstructorThisInitializer : ConstructorInitializer {
		public ConstructorThisInitializer (ArrayList argument_list, Location l) : base (argument_list, l)
		{
		}
	}
	
	public class Constructor : MethodCore {
		public ConstructorBuilder ConstructorBuilder;
		public ConstructorInitializer Initializer;
		public Attributes OptAttributes;

		// <summary>
		//   Modifiers allowed for a constructor.
		// </summary>
		const int AllowedModifiers =
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.STATIC |
			Modifiers.PRIVATE;

		//
		// The spec claims that static is not permitted, but
		// my very own code has static constructors.
		//
		public Constructor (string name, Parameters args, ConstructorInitializer init, Location l)
			: base (name, args, l)
		{
			Initializer = init;
		}

		//
		// Returns true if this is a default constructor
		//
		public bool IsDefault ()
		{
			return  (Parameters.FixedParameters == null ? true : Parameters.Empty) &&
				(Parameters.ArrayParameter == null ? true : Parameters.Empty) &&
				(Initializer is ConstructorBaseInitializer) &&
				(Initializer.Arguments == null);
		}

		//
		// Creates the ConstructorBuilder
		//
		public ConstructorBuilder Define (TypeContainer parent)
		{
			MethodAttributes ca = (MethodAttributes.RTSpecialName |
					       MethodAttributes.SpecialName);

			Type [] parameters = ParameterTypes (parent);

			if ((ModFlags & Modifiers.STATIC) != 0)
				ca |= MethodAttributes.Static;
			else {
				if (parent is Struct && parameters == null){
					Report.Error (
						568, Location, 
						"Structs can not contain explicit parameterless " +
						"constructors");
					return null;
				}
			}

			ConstructorBuilder = parent.TypeBuilder.DefineConstructor (
				ca, GetCallingConvention (parent is Class), parameters);

			//
			// HACK because System.Reflection.Emit is lame
			//
			ParameterInfo = new InternalParameters (parent, Parameters);

			if (!TypeManager.RegisterMethod (ConstructorBuilder, ParameterInfo, parameters)) {
				Report.Error (111, Location,
					      "Class `" + parent.Name + "' already contains a definition with the " +
					      "same return value and parameter types for constructor `" + Name + "'");
				return null;
			}
				
			return ConstructorBuilder;
		}

		//
		// Emits the code
		//
		public void Emit (TypeContainer parent)
		{
			ILGenerator ig = ConstructorBuilder.GetILGenerator ();
			EmitContext ec = new EmitContext (parent, Location, ig, null, ModFlags, true);

			if (parent is Class){
				if (Initializer == null)
					Initializer = new ConstructorBaseInitializer (null, parent.Location);

				if (!Initializer.Resolve (ec))
					return;
			}

			//
			// Classes can have base initializers and instance field initializers.
			//
			if (parent is Class){
				if ((ModFlags & Modifiers.STATIC) == 0)
					Initializer.Emit (ec);
				parent.EmitFieldInitializers (ec, false);
			}
			
			if ((ModFlags & Modifiers.STATIC) != 0)
				parent.EmitFieldInitializers (ec, true);

			if (OptAttributes != null) {
				if (OptAttributes.AttributeSections != null) {
					foreach (AttributeSection asec in OptAttributes.AttributeSections) {
						if (asec.Attributes == null)
							continue;
						
						foreach (Attribute a in asec.Attributes) {
							CustomAttributeBuilder cb = a.Resolve (ec);
							if (cb == null)
								continue;
							
							if (!Attribute.CheckAttribute (a, this)) {
								Attribute.Error592 (a, Location);
								return;
							}
							
							ConstructorBuilder.SetCustomAttribute (cb);
						}
					}
				}
			}

			ec.EmitTopBlock (Block);
		}
	}
	
	public class Field {
		public readonly string Type;
		public readonly Object Initializer;
		public readonly string Name;
		public readonly int    ModFlags;
		public readonly Attributes OptAttributes;
		public FieldBuilder  FieldBuilder;
		
		public Location Location;
		
		// <summary>
		//   Modifiers allowed in a class declaration
		// </summary>
		const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE |
			Modifiers.STATIC |
			Modifiers.READONLY;

		public Field (string type, int mod, string name, Object expr_or_array_init, Attributes attrs, Location loc)
		{
			Type = type;
			ModFlags = Modifiers.Check (AllowedModifiers, mod, Modifiers.PRIVATE);
			Name = name;
			Initializer = expr_or_array_init;
			OptAttributes = attrs;
			this.Location = loc;
		}

		public void Define (TypeContainer parent)
		{
			Type t = parent.LookupType (Type, false);

			if (t == null)
				return;
			
			FieldBuilder = parent.TypeBuilder.DefineField (
				Name, t, Modifiers.FieldAttr (ModFlags));
		}

		public void Emit (TypeContainer tc)
		{
			EmitContext ec = new EmitContext (tc, Location, null, FieldBuilder.FieldType, ModFlags);
			
			if (OptAttributes == null)
				return;

			if (OptAttributes.AttributeSections == null)
				return;
			
			foreach (AttributeSection asec in OptAttributes.AttributeSections) {
				if (asec.Attributes == null)
					continue;
				
				foreach (Attribute a in asec.Attributes) {
					CustomAttributeBuilder cb = a.Resolve (ec);
					if (cb == null)
						continue;
					
					if (!Attribute.CheckAttribute (a, this)) {
						Attribute.Error592 (a, Location);
						return;
					}

					FieldBuilder.SetCustomAttribute (cb);
				}
			}
		}
	}

	public class Property : MemberCore {
		public readonly string Type;
		public Block           Get, Set;
		public PropertyBuilder PropertyBuilder;
		public Attributes OptAttributes;
		MethodBuilder GetBuilder, SetBuilder;

		//
		// The type, once we compute it.
		
		Type PropertyType;

		const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE |
			Modifiers.STATIC |
			Modifiers.SEALED |
			Modifiers.OVERRIDE |
			Modifiers.ABSTRACT |
			Modifiers.VIRTUAL;

		public Property (string type, string name, int mod_flags, Block get_block, Block set_block,
				 Attributes attrs, Location loc)
			: base (name, loc)
		{
			Type = type;
			ModFlags = Modifiers.Check (AllowedModifiers, mod_flags, Modifiers.PRIVATE);
			Get = get_block;
			Set = set_block;
			OptAttributes = attrs;
		}

		public void Define (TypeContainer parent)
		{
			MethodAttributes method_attr = Modifiers.MethodAttr(ModFlags);

			if (!parent.MethodModifiersValid (ModFlags, Name, Location))
				return;

			// FIXME - PropertyAttributes.HasDefault ?

			PropertyAttributes prop_attr = PropertyAttributes.RTSpecialName |
				                       PropertyAttributes.SpecialName;
		
			// Lookup Type, verify validity
			PropertyType = parent.LookupType (Type, false);
			if (PropertyType == null)
				return;

			// verify accessibility
			if (!TypeContainer.AsAccessible (PropertyType, ModFlags))
				return;
			
			Type [] parameters = new Type [1];
			parameters [0] = PropertyType;

			//
			// Find properties with the same name on the base class
			//
			MemberInfo [] props;
			MemberInfo [] props_static = TypeContainer.FindMembers (
				parent.TypeBuilder.BaseType,
				MemberTypes.All, BindingFlags.Public | BindingFlags.Static,
				System.Type.FilterName, Name);

			MemberInfo [] props_instance = TypeContainer.FindMembers (
				parent.TypeBuilder.BaseType,
				MemberTypes.All, BindingFlags.Public | BindingFlags.Instance,
				System.Type.FilterName, Name);

			//
			// Find if we have anything
			//
			if (props_static != null && props_static.Length > 0)
				props = props_static;
			else if (props_instance != null && props_instance.Length > 0)
				props = props_instance;
			else
				props = null;

			//
			// If we have something on the base.
			if (props != null && props.Length > 0){
				//
				// FIXME:
				// Currently we expect only to get 1 match at most from our
				// base class, maybe we can get more than one, investigate
				// whether this is possible
				//
				if (props.Length > 1)
					throw new Exception ("How do we handle this?");
				
				PropertyInfo pi = (PropertyInfo) props [0];

				MethodInfo get = TypeManager.GetPropertyGetter (pi);
				MethodInfo set = TypeManager.GetPropertySetter (pi);

				MethodInfo reference = get == null ? set : get;
				
				if (!CheckMethodAgainstBase (parent, reference))
					return;
			} else {
				if ((ModFlags & Modifiers.NEW) != 0)
					WarningNotHiding (parent);
				
				if ((ModFlags & Modifiers.OVERRIDE) != 0)
					Report.Error (115, Location,
						      parent.MakeName (Name) +
						      " no suitable methods found to override");
			}
			
			PropertyBuilder = parent.TypeBuilder.DefineProperty (
				Name, prop_attr, PropertyType, null);

			if (Get != null){
				GetBuilder = parent.TypeBuilder.DefineMethod (
					"get_" + Name, method_attr, PropertyType, null);
				PropertyBuilder.SetGetMethod (GetBuilder);

				//
				// HACK because System.Reflection.Emit is lame
				//
				InternalParameters ip = new InternalParameters (
					parent, Parameters.GetEmptyReadOnlyParameters ());
				
				if (!TypeManager.RegisterMethod (GetBuilder, ip, null)) {
					Report.Error (111, Location,
					       "Class `" + parent.Name +
						      "' already contains a definition with the " +
						      "same return value and parameter types as the " +
						      "'get' method of property `" + Name + "'");
					return;
				}
			}
			
			
			if (Set != null){
				SetBuilder = parent.TypeBuilder.DefineMethod (
					"set_" + Name, method_attr, null, parameters);
				SetBuilder.DefineParameter (1, ParameterAttributes.None, "value"); 
				PropertyBuilder.SetSetMethod (SetBuilder);

				//
				// HACK because System.Reflection.Emit is lame
				//
				Parameter [] parms = new Parameter [1];
				parms [0] = new Parameter (Type, "value", Parameter.Modifier.NONE, null);
				InternalParameters ip = new InternalParameters (
					parent, new Parameters (parms, null));

				if (!TypeManager.RegisterMethod (SetBuilder, ip, parameters)) {
					Report.Error (111, Location,
					       "Class `" + parent.Name + "' already contains a definition with the " +
					       "same return value and parameter types as the " +
					       "'set' method of property `" + Name + "'");
					return;
				}
			}

			//
			// HACK for the reasons exposed above
			//
			if (!TypeManager.RegisterProperty (PropertyBuilder, GetBuilder, SetBuilder)) {
				Report.Error (
					111, Location,
					"Class `" + parent.Name + "' already contains a definition for the " +
					" property `" + Name + "'");
				return;
			}
		}
		
		public void Emit (TypeContainer tc)
		{
			ILGenerator ig;
			EmitContext ec;

			if (OptAttributes != null) {
				ec = new EmitContext (tc, Location, null, PropertyType, ModFlags);

				if (OptAttributes.AttributeSections != null) {
					foreach (AttributeSection asec in OptAttributes.AttributeSections) {
						if (asec.Attributes == null)
							continue;
						
						foreach (Attribute a in asec.Attributes) {
							CustomAttributeBuilder cb = a.Resolve (ec);
							if (cb == null)
								continue;

							if (!Attribute.CheckAttribute (a, this)) {
								Attribute.Error592 (a, Location);
								return;
							}

							PropertyBuilder.SetCustomAttribute (cb);
						}
					}
				}
			}

			//
			// abstract or extern properties have no bodies
			//
			if ((ModFlags & (Modifiers.ABSTRACT | Modifiers.EXTERN)) != 0)
				return;

			if (Get != null){
				ig = GetBuilder.GetILGenerator ();
				ec = new EmitContext (tc, Location, ig, PropertyType, ModFlags);
				
				ec.EmitTopBlock (Get);
			}

			if (Set != null){
				ig = SetBuilder.GetILGenerator ();
				ec = new EmitContext (tc, Location, ig, null, ModFlags);
				
				ec.EmitTopBlock (Set);
			}
		}
	}

	public class Event {
		
		const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE |
			Modifiers.STATIC |
			Modifiers.VIRTUAL |
			Modifiers.SEALED |
			Modifiers.OVERRIDE |
			Modifiers.ABSTRACT;

		public readonly string    Type;
		public readonly string    Name;
		public readonly Object    Initializer;
		public readonly int       ModFlags;
		public readonly Block     Add;
		public readonly Block     Remove;
		public EventBuilder       EventBuilder;
		public Attributes         OptAttributes;

		Type EventType;

		Location Location;
		
		public Event (string type, string name, Object init, int flags, Block add_block, Block rem_block,
			      Attributes attrs, Location loc)
		{
			Type = type;
			Name = name;
			Initializer = init;
			ModFlags = Modifiers.Check (AllowedModifiers, flags, Modifiers.PRIVATE);  
			Add = add_block;
			Remove = rem_block;
			OptAttributes = attrs;
			Location = loc;
		}

		public void Define (TypeContainer parent)
		{
			MethodAttributes m_attr = Modifiers.MethodAttr (ModFlags);

			EventAttributes e_attr = EventAttributes.RTSpecialName | EventAttributes.SpecialName;
			
			MethodBuilder mb;

			EventType = parent.LookupType (Type, false);
			Type [] parameters = new Type [1];
			parameters [0] = EventType;
			
			EventBuilder = parent.TypeBuilder.DefineEvent (Name, e_attr, EventType);
			
			if (Add != null) {
				mb = parent.TypeBuilder.DefineMethod ("add_" + Name, m_attr, null,
								      parameters);
				mb.DefineParameter (1, ParameterAttributes.None, "value");
				EventBuilder.SetAddOnMethod (mb);
				//
				// HACK because System.Reflection.Emit is lame
				//
				Parameter [] parms = new Parameter [1];
				parms [0] = new Parameter (Type, "value", Parameter.Modifier.NONE, null);
				InternalParameters ip = new InternalParameters (
					parent, new Parameters (parms, null)); 
				
				if (!TypeManager.RegisterMethod (mb, ip, parameters)) {
					Report.Error (111, Location,
					       "Class `" + parent.Name + "' already contains a definition with the " +
					       "same return value and parameter types for the " +
					       "'add' method of event `" + Name + "'");
					return;
				}
			}

			if (Remove != null) {
				mb = parent.TypeBuilder.DefineMethod ("remove_" + Name, m_attr, null,
								      parameters);
				mb.DefineParameter (1, ParameterAttributes.None, "value");
				EventBuilder.SetRemoveOnMethod (mb);

				//
				// HACK because System.Reflection.Emit is lame
				//
				Parameter [] parms = new Parameter [1];
				parms [0] = new Parameter (Type, "value", Parameter.Modifier.NONE, null);
				InternalParameters ip = new InternalParameters (
					parent, new Parameters (parms, null));
				
				if (!TypeManager.RegisterMethod (mb, ip, parameters)) {
					Report.Error (111, Location,	
				       "Class `" + parent.Name + "' already contains a definition with the " +
					       "same return value and parameter types for the " +
					       "'remove' method of event `" + Name + "'");
					return;
				}
			}
		}

		public void Emit (TypeContainer tc)
		{
			EmitContext ec = new EmitContext (tc, Location, null, EventType, ModFlags);

			if (OptAttributes == null)
				return;
			
			if (OptAttributes.AttributeSections == null)
				return;
			
			foreach (AttributeSection asec in OptAttributes.AttributeSections) {
				if (asec.Attributes == null)
					continue;
				
				foreach (Attribute a in asec.Attributes) {
					CustomAttributeBuilder cb = a.Resolve (ec);
					if (cb == null)
						continue;
					
					if (!Attribute.CheckAttribute (a, this)) {
						Attribute.Error592 (a, Location);
						return;
					}
					
					EventBuilder.SetCustomAttribute (cb);
				}
			}
		}
		
	}

	//
	// FIXME: This does not handle:
	//
	//   int INTERFACENAME [ args ]
	//
	// Only:
	// 
	// int this [ args ]
 
	public class Indexer {

		const int AllowedModifiers =
			Modifiers.NEW |
			Modifiers.PUBLIC |
			Modifiers.PROTECTED |
			Modifiers.INTERNAL |
			Modifiers.PRIVATE |
			Modifiers.VIRTUAL |
			Modifiers.SEALED |
			Modifiers.OVERRIDE |
			Modifiers.ABSTRACT;

		public readonly string     Type;
		public readonly string     InterfaceType;
		public readonly Parameters FormalParameters;
		public readonly int        ModFlags;
		public readonly Block      Get;
		public readonly Block      Set;
		public Attributes          OptAttributes;
		public MethodBuilder       GetBuilder;
		public MethodBuilder       SetBuilder;
		public PropertyBuilder PropertyBuilder;
	        public Type IndexerType;

		Location Location;
			
		public Indexer (string type, string int_type, int flags, Parameters parms,
				Block get_block, Block set_block, Attributes attrs, Location loc)
		{

			Type = type;
			InterfaceType = int_type;
			ModFlags = Modifiers.Check (AllowedModifiers, flags, Modifiers.PRIVATE);
			FormalParameters = parms;
			Get = get_block;
			Set = set_block;
			OptAttributes = attrs;
			Location = loc;
		}

		public void Define (TypeContainer parent)
		{
			MethodAttributes attr = Modifiers.MethodAttr (ModFlags);
			PropertyAttributes prop_attr =
				PropertyAttributes.RTSpecialName |
				PropertyAttributes.SpecialName;
			bool error = false;
			
			IndexerType = parent.LookupType (Type, false);
			Type [] parameters = FormalParameters.GetParameterInfo (parent);

			// Check if the return type and arguments were correct
			if (IndexerType == null || parameters == null)
				return;

			//
			// verify accessibility
			//
			if (!TypeContainer.AsAccessible (IndexerType, ModFlags))
				return;

			foreach (Type partype in parameters)
				if (!TypeContainer.AsAccessible (partype, ModFlags))
					error = true;

			if (error)
				return;
			
			PropertyBuilder = parent.TypeBuilder.DefineProperty (
				TypeManager.IndexerPropertyName (parent.TypeBuilder),
				prop_attr, IndexerType, parameters);

			if (Get != null){
				GetBuilder = parent.TypeBuilder.DefineMethod (
					"get_Item", attr, IndexerType, parameters);

                                InternalParameters pi = new InternalParameters (parent, FormalParameters);
				if (!TypeManager.RegisterMethod (GetBuilder, pi, parameters)) {
					Report.Error (111, Location,
						      "Class `" + parent.Name +
						      "' already contains a definition with the " +
						      "same return value and parameter types for the " +
						      "'get' indexer");
					return;
				}
			}
			
			if (Set != null){
				int top = parameters.Length;
				Type [] set_pars = new Type [top + 1];
				parameters.CopyTo (set_pars, 0);
				set_pars [top] = IndexerType;

				Parameter [] fixed_parms = FormalParameters.FixedParameters;

				Parameter [] tmp = new Parameter [fixed_parms.Length + 1];

				fixed_parms.CopyTo (tmp, 0);
				tmp [fixed_parms.Length] = new Parameter (Type, "value", Parameter.Modifier.NONE, null);

				Parameters set_formal_params = new Parameters (tmp, null);
				
				SetBuilder = parent.TypeBuilder.DefineMethod (
					"set_Item", attr, null, set_pars);
				InternalParameters ip = new InternalParameters (parent, set_formal_params);
				
				if (!TypeManager.RegisterMethod (SetBuilder, ip, set_pars)) {
					Report.Error (111, Location,
					       "Class `" + parent.Name + "' already contains a definition with the " +
					       "same return value and parameter types for the " +
					       "'set' indexer");
					return;
				}
			}

			PropertyBuilder.SetGetMethod (GetBuilder);
			PropertyBuilder.SetSetMethod (SetBuilder);
			
			Parameter [] p = FormalParameters.FixedParameters;

			if (p != null) {
				int i;
				
				for (i = 0; i < p.Length; ++i) {
					if (Get != null)
						GetBuilder.DefineParameter (
							i + 1, p [i].Attributes, p [i].Name);

					if (Set != null)
						SetBuilder.DefineParameter (
							i + 1, p [i].Attributes, p [i].Name);
				}

				if (Set != null)
					SetBuilder.DefineParameter (
						i + 1, ParameterAttributes.None, "value");
					
				if (i != parameters.Length) {
					Parameter array_param = FormalParameters.ArrayParameter;
					SetBuilder.DefineParameter (i + 1, array_param.Attributes,
								    array_param.Name);
				}
			}

			TypeManager.RegisterProperty (PropertyBuilder, GetBuilder, SetBuilder);
		}

		public void Emit (TypeContainer tc)
		{
			ILGenerator ig;
			EmitContext ec;

			if (OptAttributes != null) {
				ec = new EmitContext (tc, Location, null, IndexerType, ModFlags);
				if (OptAttributes.AttributeSections != null) {
					foreach (AttributeSection asec in OptAttributes.AttributeSections) {
						if (asec.Attributes == null)
							continue;
						
						foreach (Attribute a in asec.Attributes) {
							CustomAttributeBuilder cb = a.Resolve (ec);
							if (cb == null)
								continue;
							
							if (!Attribute.CheckAttribute (a, this)) {
								Attribute.Error592 (a, Location);
								return;
							}
							
							PropertyBuilder.SetCustomAttribute (cb);
						}
					}
				}
			}

			if (Get != null){
				ig = GetBuilder.GetILGenerator ();
				ec = new EmitContext (tc, Location, ig, IndexerType, ModFlags);
				
				ec.EmitTopBlock (Get);
			}

			if (Set != null){
				ig = SetBuilder.GetILGenerator ();
				ec = new EmitContext (tc, Location, ig, null, ModFlags);
				
				ec.EmitTopBlock (Set);
			}
		}
	}

	public class Operator {

		const int AllowedModifiers =
			Modifiers.PUBLIC |
			Modifiers.STATIC;

		const int RequiredModifiers =
			Modifiers.PUBLIC |
			Modifiers.STATIC;

		public enum OpType : byte {

			// Unary operators
			LogicalNot,
			OnesComplement,
			Increment,
			Decrement,
			True,
			False,

			// Unary and Binary operators
			Addition,
			Subtraction,

			UnaryPlus,
			UnaryNegation,
			
			// Binary operators
			Multiply,
			Division,
			Modulus,
			BitwiseAnd,
			BitwiseOr,
			ExclusiveOr,
			LeftShift,
			RightShift,
			Equality,
			Inequality,
			GreaterThan,
			LessThan,
			GreaterThanOrEqual,
			LessThanOrEqual,

			// Implicit and Explicit
			Implicit,
			Explicit
		};

		public readonly OpType OperatorType;
		public readonly string ReturnType;
		public readonly string FirstArgType;
		public readonly string FirstArgName;
		public readonly string SecondArgType;
		public readonly string SecondArgName;
		public readonly int    ModFlags;
		public readonly Block  Block;
		public Attributes      OptAttributes;
		public MethodBuilder   OperatorMethodBuilder;
		public Location        Location;
		
		public string MethodName;
		public Method OperatorMethod;

		public Operator (OpType type, string ret_type, int flags, string arg1type, string arg1name,
				 string arg2type, string arg2name, Block block, Attributes attrs, Location loc)
		{
			OperatorType = type;
			ReturnType = ret_type;
			ModFlags = Modifiers.Check (AllowedModifiers, flags, Modifiers.PUBLIC);
			FirstArgType = arg1type;
			FirstArgName = arg1name;
			SecondArgType = arg2type;
			SecondArgName = arg2name;
			Block = block;
			OptAttributes = attrs;
			Location = loc;
		}

		string Prototype (TypeContainer parent)
		{
			return parent.Name + ".operator " + OperatorType + " (" + FirstArgType + "," +
				SecondArgType + ")";
		}
		
		public void Define (TypeContainer parent)
		{
			int length = 1;
			MethodName = "op_" + OperatorType;
			
			if (SecondArgType != null)
				length = 2;
			
			Parameter [] param_list = new Parameter [length];

			if ((ModFlags & RequiredModifiers) != RequiredModifiers){
				Report.Error (
					558, Location, 
					"User defined operators `" +
					Prototype (parent) +
					"' must be declared static and public");
			}

			param_list[0] = new Parameter (FirstArgType, FirstArgName,
						       Parameter.Modifier.NONE, null);
			if (SecondArgType != null)
				param_list[1] = new Parameter (SecondArgType, SecondArgName,
							       Parameter.Modifier.NONE, null);
			
			OperatorMethod = new Method (ReturnType, ModFlags, MethodName,
						     new Parameters (param_list, null),
						     OptAttributes, Location.Null);
			
			OperatorMethod.Define (parent);
			OperatorMethodBuilder = OperatorMethod.MethodBuilder;

			Type [] param_types = OperatorMethod.ParameterTypes (parent);
			Type declaring_type = OperatorMethodBuilder.DeclaringType;
			Type return_type = OperatorMethod.GetReturnType (parent);
			Type first_arg_type = param_types [0];

			// Rules for conversion operators
			
			if (OperatorType == OpType.Implicit || OperatorType == OpType.Explicit) {
				
				if (first_arg_type == return_type && first_arg_type == declaring_type)
					Report.Error (555, Location,
					       "User-defined conversion cannot take an object of the enclosing type " +
					       "and convert to an object of the enclosing type");
				
				if (first_arg_type != declaring_type && return_type != declaring_type)
					Report.Error (556, Location, 
					       "User-defined conversion must convert to or from the enclosing type");
				
				if (first_arg_type == TypeManager.object_type || return_type == TypeManager.object_type)
					Report.Error (-8, Location,
					       "User-defined conversion cannot convert to or from object type");
				
				if (first_arg_type.IsInterface || return_type.IsInterface)
					Report.Error (552, Location,
					       "User-defined conversion cannot convert to or from an interface type");	 
				
				if (first_arg_type.IsSubclassOf (return_type) || return_type.IsSubclassOf (first_arg_type))
					Report.Error (-10, Location,
						"User-defined conversion cannot convert between types that " +
						"derive from each other"); 
				
			} else if (SecondArgType == null) {
				// Checks for Unary operators
				
				if (first_arg_type != declaring_type) 
					Report.Error (562, Location,
						   "The parameter of a unary operator must be the containing type");
				
				
				if (OperatorType == OpType.Increment || OperatorType == OpType.Decrement) {
					if (return_type != declaring_type)
						Report.Error (559, Location,
						       "The parameter and return type for ++ and -- " +
						       "must be the containing type");
					
				}
				
				if (OperatorType == OpType.True || OperatorType == OpType.False) {
					if (return_type != TypeManager.bool_type)
						Report.Error (215, Location,
						       "The return type of operator True or False " +
						       "must be bool");
				}
				
			} else {
				// Checks for Binary operators
				
				if (first_arg_type != declaring_type &&
				    param_types [1] != declaring_type)
					Report.Error (563, Location,
					       "One of the parameters of a binary operator must be the containing type");
			}
			
		
			
		}
		
		public void Emit (TypeContainer parent)
		{
			if (OptAttributes != null) {
				EmitContext ec = new EmitContext (parent, Location, null, null, ModFlags);

				if (OptAttributes.AttributeSections != null) {
					foreach (AttributeSection asec in OptAttributes.AttributeSections) {
						if (asec.Attributes == null)
							continue;
						
						foreach (Attribute a in asec.Attributes) {
							CustomAttributeBuilder cb = a.Resolve (ec);
							if (cb == null)
								continue;

							if (!Attribute.CheckAttribute (a, this)) {
								Attribute.Error592 (a, Location);
								return;
							}
							
							OperatorMethodBuilder.SetCustomAttribute (cb);
						}
					}
				}
			}
			
			OperatorMethod.Block = Block;
			OperatorMethod.Emit (parent);
		}
	}

	//
	// This is used to compare method signatures
	//
	struct MethodSignature {
		public string Name;
		public Type RetType;
		public Type [] Parameters;
		
		public MethodSignature (string name, Type ret_type, Type [] parameters)
		{
			Name = name;
			RetType = ret_type;
			Parameters = parameters;
		}
		
		public override int GetHashCode ()
		{
			return Name.GetHashCode ();
		}

		public override bool Equals (Object o)
		{
			MethodSignature other = (MethodSignature) o;

			if (other.Name != Name)
				return false;

			if (other.RetType != RetType)
				return false;
			
			if (Parameters == null){
				if (other.Parameters == null)
					return true;
				return false;
			}

			if (other.Parameters == null)
				return false;
			
			int c = Parameters.Length;
			if (other.Parameters.Length != c)
				return false;

			for (int i = 0; i < c; i++)
				if (other.Parameters [i] != Parameters [i])
					return false;

			return true;
		}
	}		
}
