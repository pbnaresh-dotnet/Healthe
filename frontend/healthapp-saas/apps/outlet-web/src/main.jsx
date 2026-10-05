
import React,{useEffect,useMemo,useState}from'react';
import{createRoot}from'react-dom/client';
import{auth,outletAdmin,catalog,currentUser,money,API_URL}from'@healthapp/shared';
import{MapContainer,TileLayer,CircleMarker,Popup,Polyline,useMap}from'react-leaflet';
import'leaflet/dist/leaflet.css';
import'./styles.css';

const ORIGIN=API_URL.replace(/\/api\/?$/,'');
const img=u=>u?(u.startsWith('http')?u:ORIGIN+u):'';
const DAYS=['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];
const SLOTS=[['Morning',1],['Afternoon',2],['Evening',3],['Night',4]];
const emptyRecipe={name:'',category:'Veg',calories:0,proteinGrams:0,carbsGrams:0,fatGrams:0,fiberGrams:0,pricePerMeal:0,largePricePerMeal:0,description:'',ingredients:[],allergenIds:[],tags:'',imageUrl:'',isActive:true};

function Modal({title,onClose,children,wide}){return <div className="overlay" onMouseDown={e=>e.target===e.currentTarget&&onClose()}><div className={wide?'modal wide':'modal'}><div className="modalHead"><h3>{title}</h3><button className="iconBtn" onClick={onClose}>×</button></div>{children}</div></div>}
function Field({label,children,help}){return <label className="field"><span>{label}</span>{children}{help&&<small>{help}</small>}</label>}
function Empty({title,text}){return <div className="empty"><div className="emptyIcon">＋</div><h3>{title}</h3><p>{text}</p></div>}
function Table({columns,rows,empty}){return rows.length?<div className="dataTable"><div className="dataRow header">{columns.map(c=><span key={c}>{c}</span>)}</div>{rows.map((r,i)=><div className="dataRow" key={i}>{r.map((v,j)=><span key={j}>{v}</span>)}</div>)}</div>:<Empty title={empty} text="There is no activity to show yet."/>}

function App(){
 const[user,setUser]=useState(currentUser()),[login,setLogin]=useState({email:'admin@fitfood.test',password:'demo'}),[active,setActive]=useState('dashboard'),[dash,setDash]=useState(null);
 const[recipes,setRecipes]=useState([]),[ingredients,setIngredients]=useState([]),[allergens,setAllergens]=useState([]),[pricing,setPricing]=useState([]),[areas,setAreas]=useState([]),[selectedAreas,setSelectedAreas]=useState([]),[tiers,setTiers]=useState([]);
 const[customers,setCustomers]=useState([]),[subs,setSubs]=useState([]),[orders,setOrders]=useState([]),[deliveries,setDeliveries]=useState([]),[menu,setMenu]=useState([]),[billing,setBilling]=useState(null),[selectedSub,setSelectedSub]=useState(null),[kitchen,setKitchen]=useState(null),[kitchenDate,setKitchenDate]=useState(new Date().toISOString().slice(0,10));
 const[drivers,setDrivers]=useState([]),[routePlan,setRoutePlan]=useState(null),[deliveryRouteDate,setDeliveryRouteDate]=useState(new Date().toISOString().slice(0,10)),[deliveryRouteMealSlot,setDeliveryRouteMealSlot]=useState(2),[selectedDriverIds,setSelectedDriverIds]=useState([]),[driverOpen,setDriverOpen]=useState(false),[driverForm,setDriverForm]=useState({firstName:'',lastName:'',email:'',password:''});
 const[error,setError]=useState(''),[busy,setBusy]=useState(false),[toast,setToast]=useState(''),[toastType,setToastType]=useState('success'),[search,setSearch]=useState(''),[category,setCategory]=useState('All'),[city,setCity]=useState('Bengaluru');
 const[recipeOpen,setRecipeOpen]=useState(false),[editRecipe,setEditRecipe]=useState(null),[recipeForm,setRecipeForm]=useState(emptyRecipe),[uploading,setUploading]=useState(false);
 const[pricingOpen,setPricingOpen]=useState(false),[priceForm,setPriceForm]=useState({maxDistanceKm:'',fee:''});
 const[tierOpen,setTierOpen]=useState(false),[editTier,setEditTier]=useState(null),[tierForm,setTierForm]=useState({minMeals:1,maxMeals:9,oneWeekPercent:0,twoWeeksPercent:0,oneMonthPercent:0});
 const[taxSettings,setTaxSettings]=useState(null),[taxForm,setTaxForm]=useState({restaurantGstRate:5,restaurantGstMode:'Exclusive'});
 const notify=(m,type='success')=>{setToast(m);setToastType(type);setTimeout(()=>setToast(''),2500)},fail=e=>setError(e?.message||'Unexpected error.');
 const load=async p=>{setBusy(true);setError('');try{
  if(p==='dashboard'){setDash(await outletAdmin.dashboard());setRecipes(await outletAdmin.recipes());setPricing(await outletAdmin.pricingRules());}
  if(p==='kitchen')setKitchen(await outletAdmin.kitchen(kitchenDate));
  if(p==='recipes'){const x=await Promise.all([outletAdmin.recipes(),catalog.ingredients(),catalog.allergens()]);setRecipes(x[0]);setIngredients(x[1]);setAllergens(x[2]);}
  if(p==='customers')setCustomers(await outletAdmin.customers());
  if(p==='subscriptions'){const x=await Promise.all([outletAdmin.subscriptions(),outletAdmin.customers()]);setSubs(x[0]);setCustomers(x[1])}
  if(p==='orders')setOrders(await outletAdmin.orders());
  if(p==='deliveries')setDeliveries(await outletAdmin.deliveries());
  if(p==='routes'){const x=await Promise.all([outletAdmin.deliveryRoutes(deliveryRouteDate,deliveryRouteMealSlot),outletAdmin.drivers()]);setRoutePlan(x[0]);setDrivers(x[1]);setSelectedDriverIds(ids=>ids.length?ids:x[1].map(d=>d.id));}
  if(p==='menu'){const x=await Promise.all([outletAdmin.menu(),outletAdmin.recipes()]);setMenu(x[0]);setRecipes(x[1])}
  if(p==='delivery-areas'){const x=await Promise.all([outletAdmin.availableDeliveryAreas(city),outletAdmin.selectedDeliveryAreas()]);setAreas(x[0]);setSelectedAreas(x[1].map(a=>a.cityAreaId))}
  if(p==='pricing')setPricing(await outletAdmin.pricingRules());
  if(p==='discounts')setTiers(await outletAdmin.discountTiers());
  if(p==='tax'){const x=await outletAdmin.taxSettings();setTaxSettings(x);setTaxForm({restaurantGstRate:x?.restaurantGstRate??5,restaurantGstMode:x?.restaurantGstMode||'Exclusive'});}
  if(p==='billing')setBilling(await outletAdmin.billing());
 }catch(e){fail(e)}finally{setBusy(false)}};
 useEffect(()=>{if(user)load(active)},[user,active]);
 useEffect(()=>{if(user&&active==='delivery-areas')load('delivery-areas')},[city]);
 const nav=p=>{setActive(p);setError('')};
 const openSubscription=async id=>{try{setBusy(true);setSelectedSub(await outletAdmin.subscriptionDetail(id))}catch(e){fail(e)}finally{setBusy(false)}};
 const refreshKitchen=async date=>{try{setBusy(true);setKitchen(await outletAdmin.kitchen(date))}catch(e){fail(e)}finally{setBusy(false)}};
 const refreshRoutes=async(date,mealSlot=deliveryRouteMealSlot)=>{try{setBusy(true);const x=await Promise.all([outletAdmin.deliveryRoutes(date,mealSlot),outletAdmin.drivers()]);setRoutePlan(x[0]);setDrivers(x[1]);setSelectedDriverIds(ids=>ids.filter(id=>x[1].some(d=>d.id===id)));}catch(e){fail(e)}finally{setBusy(false)}};
 const planRoutes=async()=>{try{if(!selectedDriverIds.length)return fail({message:'Select at least one active in-house driver.'});setBusy(true);setRoutePlan(await outletAdmin.planDeliveryRoutes({date:deliveryRouteDate,mealSlot:deliveryRouteMealSlot,driverIds:selectedDriverIds}));notify('Delivery routes planned')}catch(e){fail(e)}finally{setBusy(false)}};
 const saveDriver=async e=>{e.preventDefault();try{const d=await outletAdmin.createDriver(driverForm);setDriverOpen(false);setDriverForm({firstName:'',lastName:'',email:'',password:''});setDrivers(x=>[...x,d]);setSelectedDriverIds(x=>[...x,d.id]);notify('Driver added')}catch(e){fail(e)}};
 const signIn=async e=>{e.preventDefault();try{const x=await auth.login(login);setUser(x.user)}catch(e){fail(e)}};
 const filtered=useMemo(()=>recipes.filter(r=>(category==='All'||r.category===category)&&(!search||r.name.toLowerCase().includes(search.toLowerCase()))),[recipes,category,search]);

 const openNew=()=>{setEditRecipe(null);setRecipeForm({...emptyRecipe,ingredients:[],allergenIds:[]});setRecipeOpen(true)};
 const openEdit=r=>{setEditRecipe(r);setRecipeForm({...emptyRecipe,...r,ingredients:(r.ingredients||[]).map(x=>({ingredientId:x.ingredientId,quantity:x.quantity,unit:x.unit})),allergenIds:(r.allergens||[]).map(x=>x.id)});setRecipeOpen(true)};
 const upload=async file=>{if(!file)return;if(!file.type.startsWith('image/'))return fail({message:'Only image files are allowed.'});if(file.size>5000000)return fail({message:'Image must be 5 MB or smaller.'});try{setUploading(true);const r=await outletAdmin.uploadRecipeImage(file);setRecipeForm(f=>({...f,imageUrl:r.url}));notify('Image uploaded')}catch(e){fail(e)}finally{setUploading(false)}};
 const saveRecipe=async e=>{e.preventDefault();try{const p={...recipeForm,calories:Number(recipeForm.calories)||0,proteinGrams:Number(recipeForm.proteinGrams)||0,carbsGrams:Number(recipeForm.carbsGrams)||0,fatGrams:Number(recipeForm.fatGrams)||0,pricePerMeal:Number(recipeForm.pricePerMeal)||0,largePricePerMeal:Number(recipeForm.largePricePerMeal)||0,ingredients:(recipeForm.ingredients||[]).filter(x=>x.ingredientId&&Number(x.quantity)>0).map(x=>({ingredientId:x.ingredientId,quantity:Number(x.quantity),unit:x.unit||ingredients.find(i=>i.id===x.ingredientId)?.defaultUnit||'g'})),allergenIds:recipeForm.allergenIds||[]};if(editRecipe)await outletAdmin.updateRecipe(editRecipe.id,p);else await outletAdmin.createRecipe(p);setRecipeOpen(false);await load('recipes');notify(editRecipe?'Recipe updated':'Recipe created')}catch(e){fail(e)}};
 const removeRecipe=async r=>{if(!confirm('Delete '+r.name+'?'))return;try{await outletAdmin.deleteRecipe(r.id);await load('recipes');notify('Recipe deleted')}catch(e){fail(e)}};
 const addPrice=async e=>{e.preventDefault();const km=Number(priceForm.maxDistanceKm),fee=Number(priceForm.fee);if(km<=0)return fail({message:'Distance must be greater than 0.'});if(fee<0)return fail({message:'Fee cannot be negative.'});if(pricing.some(x=>Number(x.maxDistanceKm)===km))return fail({message:'This distance slab already exists.'});try{await outletAdmin.addPricingRule({maxDistanceKm:km,fee});setPricingOpen(false);setPriceForm({maxDistanceKm:'',fee:''});await load('pricing');notify('Delivery slab added')}catch(e){fail(e)}};
 const removePrice=async r=>{if(!confirm('Delete the '+r.maxDistanceKm+' km slab?'))return;try{await outletAdmin.deletePricingRule(r.id);await load('pricing');notify('Delivery slab removed')}catch(e){fail(e)}};
 const saveAreas=async()=>{try{await outletAdmin.saveDeliveryAreas(selectedAreas);await load('delivery-areas');notify('Delivery areas saved')}catch(e){fail(e)}};
 const saveTier=async e=>{e.preventDefault();const p={minMeals:Number(tierForm.minMeals),maxMeals:tierForm.maxMeals===''?null:Number(tierForm.maxMeals),oneWeekPercent:Number(tierForm.oneWeekPercent),twoWeeksPercent:Number(tierForm.twoWeeksPercent),oneMonthPercent:Number(tierForm.oneMonthPercent),isActive:true};try{if(editTier)await outletAdmin.updateDiscountTier(editTier.id,p);else await outletAdmin.addDiscountTier(p);setTierOpen(false);await load('discounts');notify('Discount tier saved')}catch(e){fail(e)}};
 const removeTier=async r=>{if(!confirm('Delete this discount tier?'))return;try{await outletAdmin.deleteDiscountTier(r.id);await load('discounts');notify('Discount tier removed')}catch(e){fail(e)}};
 const saveTax=async e=>{e.preventDefault();const rate=Number(taxForm.restaurantGstRate);if(!Number.isFinite(rate)||rate<0||rate>100)return fail({message:'Restaurant GST rate must be between 0% and 100%.'});try{const x=await outletAdmin.updateTaxSettings({restaurantGstRate:rate,restaurantGstMode:taxForm.restaurantGstMode});setTaxSettings(x);setTaxForm({restaurantGstRate:x?.restaurantGstRate??rate,restaurantGstMode:x?.restaurantGstMode||taxForm.restaurantGstMode});notify('Tax and GST settings saved')}catch(e){fail(e)}};

 if(!user)return <div className="loginPage"><div className="loginCard"><div className="brand"><span className="brandMark">H</span><div><b>HealthApp</b><small>Outlet management</small></div></div><h1>Welcome back</h1><p>Run your meal business from one workspace.</p><form onSubmit={signIn}><Field label="Email"><input value={login.email} onChange={e=>setLogin({...login,email:e.target.value})}/></Field><Field label="Password"><input type="password" value={login.password} onChange={e=>setLogin({...login,password:e.target.value})}/></Field><button className="primary full">Sign in</button>{error&&<div className="error">{error}</div>}<small className="demo">Demo: admin@fitfood.test / demo</small></form></div></div>;

 const navs=[['dashboard','⌂','Dashboard'],['kitchen','▦','Kitchen'],['recipes','◈','Recipes'],['menu','☷','Weekly Menu'],['customers','♙','Customers'],['subscriptions','◫','Subscriptions'],['orders','▤','Orders'],['deliveries','⌁','Deliveries'],['routes','⇢','Delivery Routes'],['delivery-areas','⌖','Delivery Areas'],['pricing','₹','Delivery Pricing'],['discounts','%','Discounts'],['tax','▤','Tax & GST'],['billing','▣','Billing']];
 const title=navs.find(n=>n[0]===active)?.[2]||'Dashboard';
 return <div className="appShell"><aside className="sidebar"><div className="sideBrand"><span className="brandMark">H</span><div><b>HealthApp</b><small>Outlet portal</small></div></div><div className="outletMini"><div className="avatar">{(dash?.outlet?.name||'F')[0]}</div><div><b>{dash?.outlet?.name||'FitFood Bengaluru'}</b><span>Outlet Admin</span></div></div><div className="navLabel">Workspace</div>{navs.slice(0,9).map(n=><button className={active===n[0]?'navItem active':'navItem'} key={n[0]} onClick={()=>nav(n[0])}><span>{n[1]}</span>{n[2]}</button>)}<div className="navLabel">Configuration</div>{navs.slice(9).map(n=><button className={active===n[0]?'navItem active':'navItem'} key={n[0]} onClick={()=>nav(n[0])}><span>{n[1]}</span>{n[2]}</button>)}<div className="sideBottom"><div className="secure">● API connected</div><button className="logoutBtn" onClick={()=>{auth.logout();setUser(null)}}>Log out</button></div></aside>
 <section className="main"><header className="topbar"><div><h2>{title}</h2><span>{dash?.outlet?.city||'Bengaluru'}, {dash?.outlet?.state||'Karnataka'}</span></div><div className="topUser"><div className="avatar sm">{(user.firstName||'A')[0]}</div><div><b>{user.firstName} {user.lastName}</b><span>{user.email}</span></div></div></header>
 <main className="content">{error&&<div className="statusBanner error"><span><b>⚠ Something needs attention</b>{error}</span><button onClick={()=>setError('')}>×</button></div>}
 {active==='dashboard'&&<Dashboard dash={dash} recipes={recipes} pricing={pricing} nav={nav} openSubscription={openSubscription}/>}
 {active==='recipes'&&<Recipes items={filtered} total={recipes.length} search={search} setSearch={setSearch} category={category} setCategory={setCategory} openNew={openNew} openEdit={openEdit} remove={removeRecipe}/>}
 {active==='menu'&&<MenuPage recipes={recipes} menu={menu} setMenu={setMenu} onSave={async()=>{try{await outletAdmin.saveMenu(menu.map(x=>({recipeId:x.recipeId,dayOfWeek:Number(x.dayOfWeek),mealSlot:Number(x.mealSlot),isAvailable:x.isAvailable,displayOrder:x.displayOrder||0})));notify('Weekly menu saved')}catch(e){fail(e)}}}/>}
 {active==='customers'&&<Page title="Customers" text="Customers connected to this outlet." content={<Table columns={['Name','Email','Role']} rows={customers.map(x=>[x.firstName+' '+x.lastName,x.email,x.role])} empty="No customers yet."/>}/>}
 {active==='subscriptions'&&<SubscriptionsPage items={subs} customers={customers} onOpen={openSubscription}/>} 
 {active==='kitchen'&&<KitchenPage data={kitchen} date={kitchenDate} setDate={setKitchenDate} refresh={refreshKitchen}/>}
 {active==='orders'&&<Page title="Orders" text="Orders generated from customer subscriptions." content={<Table columns={['Order','Customer','Status','Delivery date','Total']} rows={orders.map(x=>[String(x.id).slice(0,8)+'…',String(x.customerId).slice(0,8)+'…',x.status,new Date(x.deliveryDate).toLocaleDateString(),money(x.total)])} empty="No orders yet."/>}/>}
 {active==='deliveries'&&<Page title="Deliveries" text="Scheduled delivery jobs for this outlet." content={<Table columns={['Customer','Address','Date','Slot','Fee','Status']} rows={deliveries.map(x=>[x.customerName,x.address,new Date(x.scheduledDate).toLocaleDateString(),x.mealSlot,money(x.deliveryFee),x.status])} empty="No deliveries yet."/>}/>}
 {active==='routes'&&<DeliveryRoutesPage plan={routePlan} drivers={drivers} date={deliveryRouteDate} mealSlot={deliveryRouteMealSlot} setDate={d=>{setDeliveryRouteDate(d);refreshRoutes(d,deliveryRouteMealSlot)}} setMealSlot={s=>{setDeliveryRouteMealSlot(s);refreshRoutes(deliveryRouteDate,s)}} selectedDriverIds={selectedDriverIds} setSelectedDriverIds={setSelectedDriverIds} planRoutes={planRoutes} addDriver={()=>setDriverOpen(true)}/>}
 {active==='delivery-areas'&&<Areas city={city} setCity={setCity} areas={areas} selected={selectedAreas} setSelected={setSelectedAreas} save={saveAreas}/>}
 {active==='pricing'&&<Pricing pricing={pricing} add={()=>setPricingOpen(true)} remove={removePrice}/>}
 {active==='discounts'&&<Discounts tiers={tiers} add={()=>{setEditTier(null);setTierForm({minMeals:1,maxMeals:9,oneWeekPercent:0,twoWeeksPercent:0,oneMonthPercent:0});setTierOpen(true)}} edit={t=>{setEditTier(t);setTierForm({...t,maxMeals:t.maxMeals??''});setTierOpen(true)}} remove={removeTier}/>}
 {active==='billing'&&<Billing billing={billing}/>}
 {busy&&<div className="loadingBar"><span/></div>}</main></section>
 {toast&&<div className={'statusToast '+toastType}><span>{toastType==='success'?'✓':toastType==='warning'?'⚠':toastType==='info'?'ℹ':'×'}</span><div><b>{toastType==='success'?'Success':toastType==='warning'?'Warning':toastType==='info'?'Info':'Error'}</b><small>{toast}</small></div><button onClick={()=>setToast('')}>×</button></div>}
 {recipeOpen&&<Modal title={editRecipe?'Edit recipe':'Create new recipe'} onClose={()=>setRecipeOpen(false)} wide><RecipeForm form={recipeForm} setForm={setRecipeForm} ingredients={ingredients} allergens={allergens} upload={upload} uploading={uploading} submit={saveRecipe} cancel={()=>setRecipeOpen(false)}/></Modal>}
 {pricingOpen&&<Modal title="Add delivery pricing slab" onClose={()=>setPricingOpen(false)}><form onSubmit={addPrice} className="formGrid"><Field label="Maximum distance (km)" help="Example: 2, 5, 10, 15, 20."><input type="number" min=".1" step=".1" value={priceForm.maxDistanceKm} onChange={e=>setPriceForm({...priceForm,maxDistanceKm:e.target.value})} required/></Field><Field label="Delivery fee (₹)"><input type="number" min="0" step=".01" value={priceForm.fee} onChange={e=>setPriceForm({...priceForm,fee:e.target.value})} required/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setPricingOpen(false)}>Cancel</button><button className="primary">Add slab</button></div></form></Modal>}
 {selectedSub&&<Modal title="Subscription details" onClose={()=>setSelectedSub(null)} wide><SubscriptionDetail data={selectedSub}/></Modal>}
 {driverOpen&&<Modal title="Add in-house driver" onClose={()=>setDriverOpen(false)}><form onSubmit={saveDriver} className="formGrid"><Field label="First name"><input value={driverForm.firstName} onChange={e=>setDriverForm({...driverForm,firstName:e.target.value})} required/></Field><Field label="Last name"><input value={driverForm.lastName} onChange={e=>setDriverForm({...driverForm,lastName:e.target.value})} required/></Field><Field label="Email"><input type="email" value={driverForm.email} onChange={e=>setDriverForm({...driverForm,email:e.target.value})} required/></Field><Field label="Password"><input type="password" value={driverForm.password} onChange={e=>setDriverForm({...driverForm,password:e.target.value})} minLength="6" required/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setDriverOpen(false)}>Cancel</button><button className="primary">Add driver</button></div></form></Modal>}
 {tierOpen&&<Modal title={editTier?'Edit discount tier':''} onClose={()=>setTierOpen(false)}><form onSubmit={saveTier} className="formGrid"><Field label="Minimum meals"><input type="number" min="1" value={tierForm.minMeals} onChange={e=>setTierForm({...tierForm,minMeals:e.target.value})}/></Field><Field label="Maximum meals"><input type="number" min="1" placeholder="Blank for open-ended" value={tierForm.maxMeals} onChange={e=>setTierForm({...tierForm,maxMeals:e.target.value})}/></Field><Field label="1 week %"><input type="number" min="0" max="100" step=".01" value={tierForm.oneWeekPercent} onChange={e=>setTierForm({...tierForm,oneWeekPercent:e.target.value})}/></Field><Field label="2 weeks %"><input type="number" min="0" max="100" step=".01" value={tierForm.twoWeeksPercent} onChange={e=>setTierForm({...tierForm,twoWeeksPercent:e.target.value})}/></Field><Field label="1 month %"><input type="number" min="0" max="100" step=".01" value={tierForm.oneMonthPercent} onChange={e=>setTierForm({...tierForm,oneMonthPercent:e.target.value})}/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setTierOpen(false)}>Cancel</button><button className="primary">Save tier</button></div></form></Modal>}
 </div>
}
function Dashboard({dash,recipes,pricing,nav,openSubscription}) {
 const stats=[
  ['kitchen','Today’s meal boxes',dash?.todayMealBoxes??0],
  ['routes','Delivery points',dash?.todayDeliveryPoints??0],
  ['subscriptions','Active subscriptions',dash?.activeSubscriptions??0],
  ['customers','New customers · 7d',dash?.newCustomers??0],
  ['subscriptions','New subscriptions · 7d',dash?.newSubscriptions??0],
  ['orders','7-day sales',money(dash?.sales7d??0)]
 ];
 const slotRows=dash?.todayDeliverySlots||[];
 const pending=dash?.todayPendingDeliveries??0;
 return <div className="page">
  <div className="hero">
   <div><div className="eyebrow">OUTLET OPERATIONS</div><h1>Today at {dash?.outlet?.name||'your outlet'}</h1><p>Run production, deliveries and customer activity from one operational view.</p></div>
   <div className="heroActions"><button className="secondary" onClick={()=>nav('routes')}>Plan deliveries</button><button className="primary" onClick={()=>nav('kitchen')}>Open kitchen</button></div>
  </div>
  <div className="statGrid dashboardStats">{stats.map(s=><button className="statCard" key={s[1]} onClick={()=>nav(s[0])}><div><span>{s[1]}</span><b>{s[2]}</b></div><i>→</i></button>)}</div>

  <div className="dashboardGrid">
   <section className="panel deliveryOverview">
    <div className="panelHead"><div><h3>Today’s delivery windows</h3><p>Live workload by scheduled meal window</p></div><span className={pending>0?'pill amber':'pill green'}>{pending} pending</span></div>
    <div className="deliverySlotList">
     {slotRows.map(s=><div className="deliverySlotRow" key={s.mealSlot}>
      <div><b>{s.mealSlot}</b><span>{s.deliveryWindow}</span></div>
      <strong>{s.deliveryJobs}</strong>
      <div className="deliverySlotProgress"><span style={{width:s.deliveryJobs ? (Math.round((s.completedJobs/s.deliveryJobs)*100)+'%') : '0%'}}/></div>
      <span className="slotCompleted">{s.completedJobs} done</span>
     </div>)}
    </div>
    <div className="deliveryOverviewFooter"><span>{dash?.todayDeliveryJobs??0} delivery jobs</span><span>{dash?.todayDeliveryPoints??0} physical stops</span><button className="linkBtn" onClick={()=>nav('routes')}>Open routes →</button></div>
   </section>

   <section className="panel operationsSummary">
    <div className="panelHead"><div><h3>Operations summary</h3><p>What needs attention today</p></div></div>
    <div className="opsMetric"><div className="opsIcon">□</div><div><b>{dash?.todayMealBoxes??0}</b><span>meal boxes to prepare</span></div><button className="linkBtn" onClick={()=>nav('kitchen')}>Kitchen →</button></div>
    <div className="opsMetric"><div className="opsIcon">↗</div><div><b>{dash?.todayDeliveryJobs??0}</b><span>delivery jobs scheduled</span></div><button className="linkBtn" onClick={()=>nav('deliveries')}>Deliveries →</button></div>
    <div className="opsMetric"><div className="opsIcon">⌖</div><div><b>{dash?.todayDeliveryPoints??0}</b><span>physical delivery stops</span></div><button className="linkBtn" onClick={()=>nav('routes')}>Routes →</button></div>
    <div className="opsMetric"><div className="opsIcon">!</div><div><b>{pending}</b><span>pending deliveries</span></div><span className={pending>0?'pill amber':'pill green'}>{pending>0?'Needs action':'On track'}</span></div>
   </section>
  </div>

  <div className="twoCol">
   <section className="panel">
    <div className="panelHead"><div><h3>Latest customer activity</h3><p>Most recent subscription starts</p></div><button className="linkBtn" onClick={()=>nav('subscriptions')}>Open subscriptions →</button></div>
    {(dash?.recentSubscriptions||[]).map(s=><button className="miniRow clickable" key={s.id} onClick={()=>openSubscription(s.id)}>
      <div className="avatar sm">{(s.customerName||'C')[0]}</div>
      <div><b>{s.customerName}</b><span>{s.planName} · {s.mealCount} meals · {new Date(s.startDate).toLocaleDateString()}</span></div>
      <span className="pill green">{s.status}</span>
    </button>)}
    {!(dash?.recentSubscriptions||[]).length&&<Empty title="No recent subscription activity" text="New customer packages will appear here."/>}
   </section>

   <section className="panel">
    <div className="panelHead"><div><h3>Business pulse · last 7 days</h3><p>Commercial activity across the outlet</p></div></div>
    <div className="pulseGrid">
     <div><span>Sales</span><b>{money(dash?.sales7d??0)}</b></div>
     <div><span>Orders</span><b>{dash?.orders7d??0}</b></div>
     <div><span>New customers</span><b>{dash?.newCustomers??0}</b></div>
     <div><span>New subscriptions</span><b>{dash?.newSubscriptions??0}</b></div>
    </div>
    <div className="panelCallout"><b>Active customer base</b><span>{dash?.activeCustomers??0} customers across {dash?.activeSubscriptions??0} active subscriptions.</span></div>
   </section>
  </div>

  <div className="twoCol">
   <section className="panel"><div className="panelHead"><div><h3>Recipe catalogue</h3><p>{dash?.recipes??recipes.length} active meals customers can select</p></div><button className="linkBtn" onClick={()=>nav('recipes')}>Manage →</button></div>{recipes.slice(0,6).map(r=><div className="miniRow" key={r.id}><div className="thumb">{r.imageUrl?<img src={img(r.imageUrl)} alt=""/>:r.name[0]}</div><div><b>{r.name}</b><span>{r.category} · {r.calories} kcal · {money(r.pricePerMeal)}</span></div><span className="pill green">Active</span></div>)}{!recipes.length&&<Empty title="No recipes" text="Create your first meal."/>}</section>
   <section className="panel"><div className="panelHead"><div><h3>Delivery pricing</h3><p>Distance-based fees</p></div><button className="linkBtn" onClick={()=>nav('pricing')}>Configure →</button></div>{pricing.map(p=><div className="priceRow" key={p.id}><span>Up to {p.maxDistanceKm} km</span><b>{money(p.fee)}</b></div>)}{!pricing.length&&<Empty title="No slabs" text="Add the first pricing rule."/>}</section>
  </div>
 </div>
}
function SubscriptionsPage({items,customers,onOpen}) {
 const names=new Map(customers.map(x=>[x.id,(x.firstName+' '+x.lastName).trim()]));
 return <div className="page"><div className="pageHead"><div><h1>Subscriptions</h1><p>Customer packages with the complete meal-by-day schedule.</p></div></div>
 <section className="panel">{items.length?<div className="subscriptionList">{items.map(x=><button className="subscriptionCard" key={x.id} onClick={()=>onOpen(x.id)}>
   <div className="subscriptionMain"><div className="avatar">{(names.get(x.customerId)||'C').slice(0,1).toUpperCase()}</div><div><b>{x.planName}</b><span>{names.get(x.customerId)||'Customer'} · {String(x.customerId).slice(0,8)}…</span></div></div>
   <div><span className="pill green">{x.status}</span><b>{x.mealsPerWeek} meals</b><span>{new Date(x.nextDeliveryDate).toLocaleDateString()}</span><i>→</i></div>
 </button>)}</div>:<Empty title="No subscriptions yet" text="Customer packages will appear here."/>}</section></div>
}

function SubscriptionDetail({data}) {
 return <div className="subscriptionDetail">
  <div className="detailHeader"><div><span className="eyebrow">CUSTOMER PACKAGE</span><h2>{data.planName}</h2><p>{data.customerName} · {data.customerEmail||'No email'}</p></div><span className="pill green">{data.status}</span></div>
  <div className="detailStats">
   <div><span>Start</span><b>{new Date(data.startDate).toLocaleDateString()}</b></div><div><span>End</span><b>{new Date(data.endDate).toLocaleDateString()}</b></div><div><span>Meals</span><b>{data.mealsPerWeek}</b></div><div><span>Delivery mode</span><b>{data.deliveryMode}</b></div>
  </div>
  <div className="financialStrip"><div><span>Package meals</span><b>{money(data.mealAmount)}</b></div><div><span>Discount</span><b>− {money(data.discountAmount)}</b></div><div><span>Taxable meal value</span><b>{money(data.restaurantTaxableAmount??data.mealAmount)}</b></div><div><span>Restaurant GST · {Number(data.restaurantGstRate||0)}% ({data.restaurantGstMode||'Exclusive'})</span><b>{money(data.restaurantGstAmount)}</b></div><div><span>Package value incl. GST</span><strong>{money(data.packageAmountWithGst)}</strong></div><div><span>Delivery fees</span><b>{money(data.deliveryFee)}</b></div></div>
  <h3>Meal schedule</h3>
  <div className="scheduleTable"><div className="scheduleRow header"><span>Date</span><span>Time</span><span>Meal</span><span>Portion</span><span>Address</span><span>Status</span></div>
   {data.meals.map(m=><div className="scheduleRow" key={m.selectionId}><span>{new Date(m.mealDate).toLocaleDateString()}</span><span>{m.mealSlotName}<small>{m.deliveryWindow}</small></span><span><b>{m.mealName}</b><small>{m.category}</small></span><span>{m.portionSize}</span><span><b>{m.addressLabel||'Address'}</b><small>{m.address + (m.areaName ? ', ' + m.areaName : '') + (m.pincode ? ' ' + m.pincode : '')}</small><small>{m.contactPhone}</small></span><span className="pill">{m.status}</span></div>)}
  </div>
 </div>
}

function KitchenPage({data,date,setDate,refresh}) {
 const [printOpen,setPrintOpen]=useState(false);
 const [printMode,setPrintMode]=useState('');
 const [printSlots,setPrintSlots]=useState([2,3]);
 const slots=[
  {value:1,name:'Morning'},
  {value:2,name:'Afternoon'},
  {value:3,name:'Evening'},
  {value:4,name:'Night'}
 ];
 const selectedLabels=useMemo(()=>data?.labels?.filter(l=>printSlots.includes(Number(l.mealSlot)))||[],[data,printSlots]);
 const production=useMemo(()=>{
  const map=new Map();
  selectedLabels.forEach(l=>{
   const key=[l.mealName,l.category,l.portionSize].join('|');
   const current=map.get(key);
   map.set(key,{mealName:l.mealName,category:l.category,portionSize:l.portionSize,quantity:(current?.quantity||0)+1});
  });
  return [...map.values()].sort((a,b)=>a.mealName.localeCompare(b.mealName)||a.portionSize.localeCompare(b.portionSize));
 },[selectedLabels]);
 const toggleSlot=value=>setPrintSlots(x=>x.includes(value)?x.filter(v=>v!==value):[...x,value]);
 const doPrint=mode=>{
  if(mode==='kitchen'&&!production.length)return;
  if(mode==='labels'&&!selectedLabels.length)return;
  setPrintMode(mode);
  setPrintOpen(false);
 };
 useEffect(()=>{
  if(!printMode)return;
  const timer=setTimeout(()=>{window.print();setPrintMode('')},120);
  return()=>clearTimeout(timer);
 },[printMode]);
 return <div className="page">
  <div className="pageHead no-print"><div><span className="eyebrow">KITCHEN OPERATIONS</span><h1>Kitchen & Labels</h1><p>Daily production quantities and concise delivery labels for {new Date(date).toLocaleDateString()}.</p></div><div className="pageActions"><input type="date" value={date} onChange={e=>{setDate(e.target.value);refresh(e.target.value)}}/><button className="secondary" onClick={()=>refresh(date)}>Refresh</button><button className="primary" onClick={()=>setPrintOpen(true)} disabled={!data}>Print options</button></div></div>
  {!data?<Empty title="No kitchen report loaded" text="Select a date and refresh the report."/>:<>
   <div className="statGrid no-print">
    <div className="statCard static"><div><span>Meal boxes</span><b>{data.totalMeals}</b></div></div>
    <div className="statCard static"><div><span>Customers</span><b>{data.uniqueCustomers}</b></div></div>
    <div className="statCard static"><div><span>Subscriptions</span><b>{data.activeSubscriptions}</b></div></div>
   </div>
   <section className="panel no-print">
    <div className="panelHead"><div><h3>Production plan</h3><p>Prepare total quantities across the selected delivery windows.</p></div><span className="pill">{production.reduce((sum,x)=>sum+x.quantity,0)} meals</span></div>
    {production.length?<div className="productionTable"><div className="productionTableRow header"><span>Meal</span><span>Category</span><span>Portion</span><span>Qty</span></div>{production.map(p=><div className="productionTableRow" key={p.mealName+p.category+p.portionSize}><b>{p.mealName}</b><span>{p.category}</span><span>{p.portionSize}</span><strong>{p.quantity}</strong></div>)}</div>:<Empty title="No Afternoon / Evening meals" text="Choose another date or delivery window."/>}
   </section>
   <section className="no-print slotSummary"><span>Print window:</span>{slots.filter(s=>printSlots.includes(s.value)).map(s=><button key={s.value} className="slotChip active" onClick={()=>toggleSlot(s.value)}>{s.name} ×</button>)}{slots.filter(s=>!printSlots.includes(s.value)).map(s=><button key={s.value} className="slotChip" onClick={()=>toggleSlot(s.value)}>+ {s.name}</button>)}</section>

   <section className={printMode==='kitchen'?'kitchenPrintArea printTarget':'kitchenPrintArea'}>
    <div className="kitchenPrintHeader"><div className="healthLogo printLogo">H</div><div><b>HealthApp · Kitchen Production</b><span>{data.outletName} · {new Date(date).toLocaleDateString()} · {printSlots.map(v=>slots.find(s=>s.value===v)?.name).join(' + ')}</span></div></div>
    <table className="kitchenPrintTable"><thead><tr><th>Meal</th><th>Category</th><th>Portion</th><th>Qty</th></tr></thead><tbody>{production.map(p=><tr key={p.mealName+p.category+p.portionSize}><td>{p.mealName}</td><td>{p.category}</td><td>{p.portionSize}</td><td><b>{p.quantity}</b></td></tr>)}</tbody></table>
    <div className="kitchenPrintFooter">Production total: <b>{production.reduce((sum,x)=>sum+x.quantity,0)} meal boxes</b></div>
   </section>

   <section className={printMode==='labels'?'labelPrintArea printTarget':'labelPrintArea'}>
    <div className="labelsPrintHeader"><div className="healthLogo printLogo">H</div><div><b>{data.outletName}</b><span>Delivery labels · {new Date(date).toLocaleDateString()} · {printSlots.map(v=>slots.find(s=>s.value===v)?.name).join(' + ')}</span></div></div>
    <div className="labelGrid">{selectedLabels.map(l=><article className="labelCard" key={l.selectionId}>
      <div className="labelTop"><div className="labelOutletBrand">{l.logoUrl?<img src={img(l.logoUrl)} alt=""/>:<div className="outletLogo">{(l.outletName||'O')[0]}</div>}<b>{l.outletName}</b></div><span>{l.subscriptionPlanName}</span></div>
      <div className="labelCustomerCompact"><span>{l.addressLabel||'DELIVERY'}</span><b>{l.customerName}</b></div>
      <div className="labelMealCompact"><b>{l.mealName}</b><span>{l.category} · {l.portionSize}</span></div>
      <div className="labelMetaRow"><span>{l.mealSlotName}</span><b>{l.deliveryWindow}</b></div>
      <div className="labelAddressCompact"><b>{l.address}</b><span>{[l.areaName,l.pincode].filter(Boolean).join(' · ')}</span>{l.customerPhone&&<span>☎ {l.customerPhone}</span>}</div>
      <div className="labelFooterCompact"><span>HealthApp</span><b>#{String(l.selectionId).slice(0,6).toUpperCase()}</b></div>
    </article>)}</div>
   </section>
  </>}
  {printOpen&&<Modal title="Print options" onClose={()=>setPrintOpen(false)}>
   <div className="printOptions">
    <div className="printOptionBlock"><div><b>Delivery windows</b><small>Select the meal windows for both the kitchen sheet and labels.</small></div><div className="printSlotChoices">{slots.map(s=><button type="button" key={s.value} className={printSlots.includes(s.value)?'printSlotChoice checked':'printSlotChoice'} onClick={()=>toggleSlot(s.value)}>{printSlots.includes(s.value)?'✓':'+'} {s.name}</button>)}</div></div>
    <div className="printChoice"><div><b>Kitchen production sheet</b><small>One consolidated table: meal, category, portion and total quantity.</small></div><button className="primary" onClick={()=>doPrint('kitchen')} disabled={!production.length}>Print kitchen</button></div>
    <div className="printChoice"><div><b>Meal-box labels</b><small>{selectedLabels.length} concise labels for the selected windows.</small></div><button className="primary" onClick={()=>doPrint('labels')} disabled={!selectedLabels.length}>Print labels</button></div>
   </div>
  </Modal>}
 </div>
}

function MapBounds({coords}){const map=useMap();useEffect(()=>{if(!coords.length)return;const bounds=coords.map(x=>[x[0],x[1]]);map.fitBounds(bounds,{padding:[30,30],maxZoom:13})},[map,coords]);return null}
function DeliveryRoutesPage({plan,drivers,date,mealSlot,setDate,setMealSlot,selectedDriverIds,setSelectedDriverIds,planRoutes,addDriver}){
 const routeColor=i=>`hsl(${(i*67)%360} 65% 42%)`;
 const assigned=new Set((plan?.routes||[]).flatMap(r=>r.stops.map(s=>s.addressId)));
 const bounds=plan?[[plan.outletLatitude,plan.outletLongitude],...(plan.points||[]).filter(p=>!assigned.has(p.addressId)).map(p=>[p.latitude,p.longitude]),...(plan.routes||[]).flatMap(r=>(r.geometry||[]).map(p=>[p[1],p[0]]))]:[];
 const slot=SLOTS.find(s=>s[1]===Number(mealSlot))||SLOTS[1];
 const slotName=slot[0];
 const slotWindow=slotName==='Afternoon'?'12:00–14:00':slotName==='Evening'?'17:00–19:00':slotName==='Morning'?'07:00–09:00':'20:00–22:00';
 const points=plan?.points||[];
 const unassigned=points.filter(p=>!assigned.has(p.addressId));
 return <div className="page">
  <div className="pageHead"><div><span className="eyebrow">LAST-MILE OPERATIONS</span><h1>Delivery Routes</h1><p>Choose a delivery window, review every stop on the map, then calculate road routes across the selected drivers.</p></div><div className="pageActions"><input type="date" value={date} onChange={e=>setDate(e.target.value)}/><button className="secondary" onClick={addDriver}>+ Driver</button><button className="primary" onClick={planRoutes} disabled={!drivers.length||!points.length}>Optimize routes</button></div></div>
  <section className="panel routeControls">
   <div className="panelHead"><div><h3>Delivery window</h3><p>{slotName} deliveries for {new Date(date+'T00:00:00').toLocaleDateString()} · {slotWindow}</p></div><span className="count">{plan?.totalDeliveryPoints||0} stops · {plan?.totalDeliveries||0} delivery jobs</span></div>
   <div className="routeSlotPicker">{SLOTS.map(s=><button type="button" key={s[1]} className={Number(mealSlot)===s[1]?'routeSlotChoice active':'routeSlotChoice'} onClick={()=>setMealSlot(s[1])}><b>{s[0]}</b><small>{s[0]==='Morning'?'07:00–09:00':s[0]==='Afternoon'?'12:00–14:00':s[0]==='Evening'?'17:00–19:00':'20:00–22:00'}</small></button>)}</div>
   <div className="panelHead routeDriverHead"><div><h3>Drivers</h3><p>Each driver receives a geographically grouped road route. Multiple delivery jobs at the same address stay together as one physical stop.</p></div><span className="count">{selectedDriverIds.length} selected · {drivers.length} active</span></div>
   <div className="driverPicker">{drivers.map(d=><label key={d.id} className={selectedDriverIds.includes(d.id)?'driverChoice checked':'driverChoice'}><input type="checkbox" checked={selectedDriverIds.includes(d.id)} onChange={e=>setSelectedDriverIds(e.target.checked?[...selectedDriverIds,d.id]:selectedDriverIds.filter(x=>x!==d.id))}/><span><b>{d.name}</b><small>{d.email}</small></span></label>)}</div>
   {!drivers.length&&<div className="notice"><b>No drivers yet.</b><span>Add an in-house driver, then calculate the route.</span></div>}
  </section>
  {!plan?<Empty title="No route plan loaded" text="Select a date and delivery window to load delivery points."/>:<>
   <div className="statGrid routeStats"><div className="statCard static"><div><span>Delivery points</span><b>{plan.totalDeliveryPoints}</b></div></div><div className="statCard static"><div><span>Delivery jobs</span><b>{plan.totalDeliveries}</b></div></div><div className="statCard static"><div><span>Driver routes</span><b>{plan.routes?.length||0}</b></div></div><div className="statCard static"><div><span>Unassigned</span><b>{plan.unassignedPoints}</b></div></div></div>
   <section className="mapRouteGrid">
    <div className="panel mapPanel"><div className="panelHead"><div><h3>{plan.outletName} → delivery points</h3><p>{slotName} · {slotWindow} · blue marker is the outlet; every delivery point represents one address.</p></div><span className="routeLegend"><i/>Outlet <em/>Unassigned</span></div>
     <MapContainer center={[plan.outletLatitude,plan.outletLongitude]} zoom={12} scrollWheelZoom className="deliveryMap">
      <TileLayer attribution="&copy; OpenStreetMap contributors" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"/>
      <MapBounds coords={bounds}/>
      <CircleMarker center={[plan.outletLatitude,plan.outletLongitude]} radius={12} pathOptions={{color:'#14532d',fillColor:'#14532d',fillOpacity:1,weight:3}}><Popup><b>{plan.outletName}</b><br/>Dispatch / outlet location<br/><small>{plan.totalDeliveryPoints} delivery points · {plan.totalDeliveries} delivery jobs</small></Popup></CircleMarker>
      {(plan.routes||[]).map((route,ri)=><React.Fragment key={route.id}><Polyline positions={(route.geometry||[]).map(p=>[p[1],p[0]])} pathOptions={{color:routeColor(ri),weight:6,opacity:.8}}/>{(route.stops||[]).map(stop=><CircleMarker key={stop.id} center={[stop.latitude,stop.longitude]} radius={9} pathOptions={{color:routeColor(ri),fillColor:routeColor(ri),fillOpacity:.9,weight:2}}><Popup><b>Stop {stop.stopSequence} · {stop.customerName}</b><br/>{stop.deliveryCount} delivery job{stop.deliveryCount===1?'':'s'} at this address.<br/>{stop.address}<br/><small>{route.driverName} · {route.mealSlot} · {route.deliveryWindow}</small></Popup></CircleMarker>)}</React.Fragment>)}
      {unassigned.map(p=><CircleMarker key={p.deliveryId} center={[p.latitude,p.longitude]} radius={8} pathOptions={{color:'#64748b',fillColor:'#64748b',fillOpacity:.85,weight:2}}><Popup><b>{p.customerName}</b><br/>{p.deliveryCount} delivery job{p.deliveryCount===1?'':'s'} at this address.<br/>{p.address}<br/><small>Unassigned · {p.mealSlot}</small></Popup></CircleMarker>)}
     </MapContainer>
     <div className="mapNote">Map tiles: OpenStreetMap · Road routing: {plan.routes?.[0]?.routingSource||'not calculated yet'} · Optimization: {plan.optimizationSource||'not calculated yet'} · planned travel {plan.plannedDistanceKm||0} km / ≈ {Math.round(plan.plannedDurationMinutes||0)} min. OR-Tools assigns stops across drivers using OSRM road travel times; OSRM draws the final road geometry.</div>
    </div>
    <div className="panel routeListPanel"><div className="panelHead"><div><h3>Driver routes</h3><p>Routes are calculated separately for {slotName}. One physical stop can carry multiple delivery jobs.</p></div></div>
     {(plan.routes||[]).map((route,ri)=><article className="routeCard" key={route.id}><div className="routeCardHead"><div><span className="routeColor" style={{background:routeColor(ri)}}/><div><b>{route.driverName}</b><small>Route {ri+1} · {route.mealSlot} · {route.deliveryWindow} · {route.stops.length} stops</small></div></div><span className="pill green">{route.status}</span></div><div className="routeMetrics"><b>{route.totalDistanceKm} km</b><span>≈ {Math.round(route.totalDurationMinutes)} min</span><span>{route.routingSource}</span></div><div className="stopList">{route.stops.map(stop=><div className="stopRow" key={stop.id}><strong>{stop.stopSequence}</strong><div><b>{stop.customerName}</b><span>{stop.deliveryCount} delivery job{stop.deliveryCount===1?'':'s'} · {stop.address}</span></div></div>)}</div></article>)}
     {!(plan.routes||[]).length&&<Empty title="Routes not calculated yet" text={points.length?"Choose drivers and calculate the "+slotName.toLowerCase()+" routes.":"There are no eligible deliveries for this window."}/>}
    </div>
   </section>
  </>}
 </div>
}
function Page({title,text,content}){return <div className="page"><div className="pageHead"><div><h1>{title}</h1><p>{text}</p></div></div><section className="panel">{content}</section></div>}
function Recipes({items,total,search,setSearch,category,setCategory,openNew,openEdit,remove}){return <div className="page"><div className="pageHead"><div><h1>Recipes</h1><p>Create meals with DB-backed ingredient quantities and allergy metadata.</p></div><button className="primary" onClick={openNew}>+ New recipe</button></div><div className="toolbar"><input className="search" value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search recipes…"/><select value={category} onChange={e=>setCategory(e.target.value)}><option>All</option><option>Veg</option><option>NonVeg</option><option>Vegan</option><option>Eggetarian</option><option>Pescatarian</option></select><span className="count">{items.length} / {total}</span></div>{!items.length?<Empty title="No matching recipes" text="Try another filter or create a recipe."/>:<div className="recipeGrid">{items.map(r=><article className="recipeCard" key={r.id}><div className="recipeImage">{r.imageUrl?<img src={img(r.imageUrl)} alt=""/>:<div className="placeholderImage">{r.name[0]}</div>}<span className="badge green">Active</span></div><div className="recipeBody"><div className="recipeTitle"><div><h3>{r.name}</h3><small>{r.category}</small></div><b>{money(r.pricePerMeal)}</b></div><p>{r.description||'No description yet.'}</p><div className="nutrition"><span><b>{r.calories}</b>kcal</span><span><b>{r.proteinGrams}g</b>protein</span><span><b>{r.carbsGrams}g</b>carbs</span><span><b>{r.fatGrams}g</b>fat</span></div><div className="recipeMeta"><span>{r.ingredients?.length||0} ingredients</span><span>{r.allergens?.length||0} allergy flags</span></div><div className="cardActions"><button className="secondary" onClick={()=>openEdit(r)}>Edit</button><button className="dangerText" onClick={()=>remove(r)}>Delete</button></div></div></article>)}</div>}</div>}
function RecipeForm({form,setForm,ingredients,allergens,upload,uploading,submit,cancel}){const set=(k,v)=>setForm(f=>({...f,[k]:v}));const addIngredient=()=>set('ingredients',[...(form.ingredients||[]),{ingredientId:ingredients[0]?.id||'',quantity:100,unit:ingredients[0]?.defaultUnit||'g'}]);const updateIngredient=(idx,k,v)=>set('ingredients',(form.ingredients||[]).map((x,i)=>i===idx?{...x,[k]:v}:x));const removeIngredient=idx=>set('ingredients',(form.ingredients||[]).filter((_,i)=>i!==idx));const toggleAllergen=id=>set('allergenIds',(form.allergenIds||[]).includes(id)?(form.allergenIds||[]).filter(x=>x!==id):[...(form.allergenIds||[]),id]);return <form onSubmit={submit}><div className="recipeEditor"><div className="imageUpload"><div className="uploadPreview">{form.imageUrl?<img src={img(form.imageUrl)} alt="preview"/>:<div><span>＋</span><p>Upload meal photo</p><small>JPG, PNG, WEBP · 5 MB max</small></div>}</div><label className="uploadBtn">{uploading?'Uploading…':form.imageUrl?'Replace image':'Upload image'}<input type="file" accept="image/jpeg,image/png,image/webp" onChange={e=>upload(e.target.files?.[0])}/></label></div><div className="formGrid"><Field label="Recipe name"><input value={form.name} onChange={e=>set('name',e.target.value)} required/></Field><Field label="Category"><select value={form.category} onChange={e=>set('category',e.target.value)}><option>Veg</option><option>NonVeg</option><option>Vegan</option><option>Eggetarian</option><option>Pescatarian</option></select></Field><Field label="Regular price (₹)"><input type="number" min="0" step=".01" value={form.pricePerMeal} onChange={e=>set('pricePerMeal',e.target.value)}/></Field><Field label="Large price (₹)"><input type="number" min="0" step=".01" value={form.largePricePerMeal} onChange={e=>set('largePricePerMeal',e.target.value)}/></Field><Field label="Calories"><input type="number" min="0" value={form.calories} onChange={e=>set('calories',e.target.value)}/></Field><Field label="Protein (g)"><input type="number" min="0" value={form.proteinGrams} onChange={e=>set('proteinGrams',e.target.value)}/></Field><Field label="Carbs (g)"><input type="number" min="0" value={form.carbsGrams} onChange={e=>set('carbsGrams',e.target.value)}/></Field><Field label="Fat (g)"><input type="number" min="0" value={form.fatGrams} onChange={e=>set('fatGrams',e.target.value)}/></Field><Field label="Fiber (g)"><input type="number" min="0" value={form.fiberGrams??0} onChange={e=>set('fiberGrams',e.target.value)}/></Field><div className="span2"><Field label="Description"><textarea value={form.description} onChange={e=>set('description',e.target.value)} placeholder="Describe the meal…"/></Field></div><div className="span2 ingredientEditor"><div className="editorTitle"><div><b>Ingredients</b><small>Choose the ingredient from the master list and enter the quantity used in this recipe.</small></div><button type="button" className="secondary small" onClick={addIngredient}>+ Add ingredient</button></div>{(form.ingredients||[]).map((x,idx)=><div className="ingredientRow" key={idx}><select value={x.ingredientId} onChange={e=>{const item=ingredients.find(i=>i.id===e.target.value);updateIngredient(idx,'ingredientId',e.target.value);if(item)updateIngredient(idx,'unit',item.defaultUnit)}}><option value="">Choose ingredient</option>{ingredients.map(i=><option key={i.id} value={i.id}>{i.name}</option>)}</select><input type="number" min="0.001" step="0.001" value={x.quantity} onChange={e=>updateIngredient(idx,'quantity',e.target.value)} placeholder="Quantity"/><input value={x.unit} onChange={e=>updateIngredient(idx,'unit',e.target.value)} placeholder="Unit"/><button type="button" className="dangerText" onClick={()=>removeIngredient(idx)}>Remove</button></div>)}{!(form.ingredients||[]).length&&<div className="editorEmpty">No ingredients added. Example: Chicken Breast · 100 g · Olive Oil · 20 g.</div>}</div><div className="span2 allergyEditor"><div className="editorTitle"><div><b>Recipe-level allergens</b><small>These are additional explicit allergens. Ingredient-linked allergens are calculated automatically.</small></div></div><div className="allergenChoices">{allergens.map(a=><label key={a.id} className={(form.allergenIds||[]).includes(a.id)?'allergenChoice checked':'allergenChoice'}><input type="checkbox" checked={(form.allergenIds||[]).includes(a.id)} onChange={()=>toggleAllergen(a.id)}/><span>{a.name}</span></label>)}</div>{!allergens.length&&<div className="editorEmpty">No allergen master values available.</div>}</div><Field label="Tags"><input value={form.tags||''} onChange={e=>set('tags',e.target.value)} placeholder="High protein, low carb…"/></Field><Field label="Image URL"><input value={form.imageUrl||''} onChange={e=>set('imageUrl',e.target.value)} placeholder="https://…"/></Field></div></div><div className="modalActions"><button type="button" className="secondary" onClick={cancel}>Cancel</button><button className="primary" disabled={uploading}>{form.id?'Save changes':'Create recipe'}</button></div></form>}
function MenuPage({recipes,menu,setMenu,onSave}){const[day,setDay]=useState(1),[slot,setSlot]=useState(1),[rid,setRid]=useState('');useEffect(()=>{if(!rid&&recipes[0])setRid(recipes[0].id)},[recipes,rid]);const add=()=>{const r=recipes.find(x=>x.id===rid);if(!r)return;if(menu.some(x=>Number(x.dayOfWeek)===day&&Number(x.mealSlot)===slot&&x.recipeId===rid))return;setMenu([...menu,{id:'tmp-'+Date.now(),recipeId:rid,recipeName:r.name,dayOfWeek:day,mealSlot:slot,isAvailable:true,displayOrder:0}])};return <div className="page"><div className="pageHead"><div><h1>Weekly Menu</h1><p>Configure which meals are available on each day and slot.</p></div><button className="primary" onClick={onSave}>Save weekly menu</button></div><div className="menuControls"><select value={day} onChange={e=>setDay(Number(e.target.value))}>{DAYS.map((d,i)=><option value={i} key={d}>{d}</option>)}</select><select value={slot} onChange={e=>setSlot(Number(e.target.value))}>{SLOTS.map(s=><option value={s[1]} key={s[1]}>{s[0]}</option>)}</select><select value={rid} onChange={e=>setRid(e.target.value)}><option value="">Choose recipe…</option>{recipes.map(r=><option value={r.id} key={r.id}>{r.name}</option>)}</select><button className="secondary" onClick={add}>+ Add item</button></div><div className="menuWeek">{DAYS.map((d,i)=><section className={i===day?'dayCard selected':'dayCard'} key={d} onClick={()=>setDay(i)}><div className="dayHead"><b>{d}</b><span>{menu.filter(m=>Number(m.dayOfWeek)===i&&m.isAvailable).length} available</span></div>{menu.filter(m=>Number(m.dayOfWeek)===i).map(m=><div className="menuItem" key={m.id}><div><b>{m.recipeName||((recipes.find(r=>r.id===m.recipeId)||{}).name)||'Meal'}</b><span>{((SLOTS.find(s=>s[1]===Number(m.mealSlot))||[])[0])||m.mealSlot}</span></div><div><input type="checkbox" checked={m.isAvailable} onChange={e=>setMenu(menu.map(x=>x.id===m.id?{...x,isAvailable:e.target.checked}:x))}/><button className="iconBtn small" onClick={e=>{e.stopPropagation();setMenu(menu.filter(x=>x.id!==m.id))}}>×</button></div></div>)}{!menu.some(m=>Number(m.dayOfWeek)===i)&&<div className="dayEmpty">No items</div>}</section>)}</div></div>}
function Areas({city,setCity,areas,selected,setSelected,save}){return <div className="page"><div className="pageHead"><div><h1>Delivery Areas</h1><p>Select the master city areas this outlet serves.</p></div><button className="primary" onClick={save}>Save selected</button></div><div className="notice"><b>Customer address rule:</b><span>Only selected areas can be used for this outlet.</span></div><div className="toolbar"><input value={city} onChange={e=>setCity(e.target.value)} placeholder="City"/><span className="count">{selected.length} selected</span></div><div className="areaGrid">{areas.map(a=><label className={selected.includes(a.id)?'areaCard checked':'areaCard'} key={a.id}><input type="checkbox" checked={selected.includes(a.id)} onChange={e=>setSelected(e.target.checked?[...selected,a.id]:selected.filter(x=>x!==a.id))}/><div><b>{a.name}</b><span>{a.city}, {a.state}</span><small>{a.pincode}</small></div></label>)}</div></div>}
function Pricing({pricing,add,remove}){return <div className="page"><div className="pageHead"><div><h1>Delivery Pricing</h1><p>Configure distance-based delivery fees for customers.</p></div><button className="primary" onClick={add}>+ Add distance slab</button></div><div className="pricingExplain"><span>Customer address</span><b>→</b><span>Distance calculated</span><b>→</b><span>Matching slab fee</span></div><section className="panel">{pricing.length?<div className="pricingTable"><div className="ptHead"><span>Up to distance</span><span>Fee</span><span>Action</span></div>{pricing.map(p=><div className="ptRow" key={p.id}><b>≤ {p.maxDistanceKm} km</b><strong>{money(p.fee)}</strong><button className="dangerText" onClick={()=>remove(p)}>Delete</button></div>)}</div>:<Empty title="No pricing slabs" text="Start with 2 km = ₹10, then add larger distance slabs."/>}</section></div>}
function Discounts({tiers,add,edit,remove}){return <div className="page"><div className="pageHead"><div><h1>Subscription Discounts</h1><p>Discounts based on committed meal quantity and package duration.</p></div><button className="primary" onClick={add}>+ Add tier</button></div><section className="panel"><div className="discountTable"><div className="dtRow header"><span>Meals</span><span>1W</span><span>2W</span><span>1M</span><span>Actions</span></div>{tiers.map(t=><div className="dtRow" key={t.id}><b>{t.minMeals} – {t.maxMeals??'∞'}</b><span>{t.oneWeekPercent}%</span><span>{t.twoWeeksPercent}%</span><span>{t.oneMonthPercent}%</span><div><button className="secondary smallBtn" onClick={()=>edit(t)}>Edit</button><button className="dangerText" onClick={()=>remove(t)}>Delete</button></div></div>)}</div>{!tiers.length&&<Empty title="No discount tiers" text="Create your first quantity-based discount."/>}</section></div>}
function TaxSettingsPage({settings,form,setForm,save}){const rate=Number(form.restaurantGstRate)||0;const sample=236;const taxable=form.restaurantGstMode==='Inclusive'&&rate>=0?Math.round(sample*100/(100+rate)*100)/100:sample;const gst=form.restaurantGstMode==='Inclusive'?Math.round((sample-taxable)*100)/100:Math.round(sample*rate/100*100)/100;return <div className="page"><div className="pageHead"><div><span className="eyebrow">TAX CONFIGURATION</span><h1>Tax & GST</h1><p>Configure how meal prices are represented to customers. The backend uses this setting for package quotes and financial snapshots.</p></div></div><div className="taxSettingsGrid"><section className="panel"><div className="panelHead"><div><h3>Restaurant GST</h3><p>Choose whether your configured meal prices include GST.</p></div>{settings&&<span className="pill green">Saved · {settings.restaurantGstMode}</span>}</div><form onSubmit={save} className="formGrid"><Field label="Restaurant GST rate" help="Enter the outlet's applicable GST percentage."><input type="number" min="0" max="100" step=".01" value={form.restaurantGstRate} onChange={e=>setForm({...form,restaurantGstRate:e.target.value})} required/></Field><div className="span2"><span className="field"><span>Price treatment</span><div className="taxModeCards"><button type="button" className={form.restaurantGstMode==='Exclusive'?'taxModeCard selected':'taxModeCard'} onClick={()=>setForm({...form,restaurantGstMode:'Exclusive'})}><b>GST Exclusive</b><span>Meal price is before GST. GST is added at checkout.</span></button><button type="button" className={form.restaurantGstMode==='Inclusive'?'taxModeCard selected':'taxModeCard'} onClick={()=>setForm({...form,restaurantGstMode:'Inclusive'})}><b>GST Inclusive</b><span>Meal price already includes GST. GST is extracted and reported separately.</span></button></div></span></div><div className="modalActions"><button className="primary">Save tax settings</button></div></form></section><aside className="panel taxPreview"><span className="eyebrow">LIVE EXAMPLE</span><h3>{form.restaurantGstMode==='Inclusive'?'Inclusive price breakdown':'Exclusive price breakdown'}</h3><p>Example meal price: <b>₹{sample.toFixed(2)}</b> at {rate}% GST.</p><div className="taxPreviewRows"><div><span>{form.restaurantGstMode==='Inclusive'?'Displayed / gross price':'Meal price before GST'}</span><b>{money(sample)}</b></div><div><span>Taxable value</span><b>{money(taxable)}</b></div><div><span>Restaurant GST</span><b>{money(gst)}</b></div><div className="total"><span>Customer meal amount</span><strong>{money(form.restaurantGstMode==='Inclusive'?sample:sample+gst)}</strong></div></div><small>{form.restaurantGstMode==='Inclusive'?'The customer continues to see the configured ₹236 price; reports split it into taxable value and GST.':'GST is added on top of the configured meal price at checkout.'}</small></aside></div></div>}
function Billing({billing}){return <div className="page"><div className="pageHead"><div><h1>Billing</h1><p>Current outlet SaaS subscription and commission details.</p></div></div>{billing?<div className="billingGrid"><section className="panel"><span className="eyebrow">CURRENT PLAN</span><h2>{billing.planName}</h2><div className="bigMoney">{money(billing.subscriptionFee)}<small> / {billing.billingCycle}</small></div><div className="kv"><span>Commission</span><b>{billing.transactionFeePercent}%</b></div><div className="kv"><span>Included customers</span><b>{billing.includedActiveCustomers}</b></div><div className="kv"><span>Active customers</span><b>{billing.activeCustomers}</b></div></section><section className="panel"><h3>Plan details</h3><div className="kv"><span>Setup fee</span><b>{money(billing.setupFee)}</b></div><div className="kv"><span>Renewal</span><b>{new Date(billing.renewalDate).toLocaleDateString()}</b></div><div className="kv"><span>Status</span><span className="pill green">{billing.status}</span></div></section></div>:<Empty title="Billing unavailable" text="The outlet has no active SaaS subscription."/>}</div>}
createRoot(document.getElementById('root')).render(<App/>);
