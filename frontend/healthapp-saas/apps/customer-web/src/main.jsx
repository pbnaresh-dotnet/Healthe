import React,{useEffect,useMemo,useRef,useState}from'react';
import{createRoot}from'react-dom/client';
import{auth,outlets,locations,recipes,menu,customer,catalog,money,currentUser,API_URL}from'@healthapp/shared';
import{MapContainer,TileLayer,CircleMarker,useMap,useMapEvents}from'react-leaflet';
import'leaflet/dist/leaflet.css';
import'./styles.css';

const SLOT=[{id:1,label:'Morning',icon:'☀️'},{id:2,label:'Afternoon',icon:'🥗'},{id:3,label:'Evening',icon:'🍲'},{id:4,label:'Night',icon:'🌙'}];
const DAYS=[{id:1,label:'Monday',short:'Mon'},{id:2,label:'Tuesday',short:'Tue'},{id:3,label:'Wednesday',short:'Wed'},{id:4,label:'Thursday',short:'Thu'},{id:5,label:'Friday',short:'Fri'},{id:6,label:'Saturday',short:'Sat'},{id:0,label:'Sunday',short:'Sun'}];
const DURATIONS=[{id:'ThreeDays',label:'3 days',days:3,weeks:1},{id:'FiveDays',label:'5 days',days:5,weeks:1},{id:'OneWeek',label:'7 days · 1 week',days:7,weeks:1},{id:'TwoWeeks',label:'2 weeks',days:14,weeks:2},{id:'OneMonth',label:'4 weeks',days:28,weeks:4}];
const GOALS=[['WeightLoss','Weight Loss'],['MuscleGain','Muscle Gain'],['GLP1Support','GLP-1 Support'],['HighPerformance','High Performance']];
const DIETS=['Veg','NonVeg','Vegan','Eggetarian','Pescatarian'];
const ACTIVITY=[['Sedentary','Sedentary'],['Light','Lightly active'],['Moderate','Moderately active'],['High','Highly active'],['Athlete','Athlete']];
const CATEGORIES=['All','Veg','NonVeg','Vegan','Eggetarian','Pescatarian'];
const todayISO=()=>new Date().toISOString().slice(0,10);
const nextMonday=()=>{const d=new Date();const n=((8-(d.getDay()||7))%7)||7;d.setDate(d.getDate()+n);return d.toISOString().slice(0,10)};
const addDays=(iso,n)=>{const d=new Date(iso+'T00:00:00');d.setDate(d.getDate()+n);return d.toISOString().slice(0,10)};
const dateObj=iso=>new Date(iso+'T00:00:00');
const normalizeMealDate=value=>String(value??'').slice(0,10);
const weekStartForDate=iso=>{const d=dateObj(normalizeMealDate(iso));const day=d.getDay();d.setDate(d.getDate()-(day===0?6:day-1));return d.toISOString().slice(0,10)};
const formatDate=iso=>new Intl.DateTimeFormat('en-IN',{day:'2-digit',month:'short',year:'numeric'}).format(dateObj(iso));
const shortDate=iso=>new Intl.DateTimeFormat('en-IN',{day:'2-digit',month:'short'}).format(dateObj(iso));
const dayId=iso=>dateObj(iso).getDay();
const slotName=id=>SLOT.find(x=>x.id===Number(id))?.label||'Meal';
const dayName=id=>DAYS.find(x=>x.id===id)?.label||'Day';
const key=(date,slot)=>date+'_'+slot;
const IMAGE_FALLBACKS={hero:'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=1400&q=85',logo:'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=240&q=85',veg:'https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=900&q=85',nonveg:'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=85',vegan:'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=85'};
const getImg=u=>u?((u.startsWith('http://')||u.startsWith('https://'))?u:(API_URL?(new URL(API_URL).origin+u):null)):null;
const fallbackImg=(category='')=>{const k=String(category).toLowerCase();return k.includes('vegan')?IMAGE_FALLBACKS.vegan:k.includes('non')?IMAGE_FALLBACKS.nonveg:IMAGE_FALLBACKS.veg};
const blankWeeks={1:[1,2,3,4,5]};
const MAP_TILE_URL=import.meta.env.VITE_MAP_TILE_URL||'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png';
const MAP_ATTRIBUTION=import.meta.env.VITE_MAP_ATTRIBUTION||'&copy; OpenStreetMap contributors';
const INDIA_MAP_CENTER=[20.5937,78.9629];
const CITY_MAP_CENTERS={
  Chennai:[13.0827,80.2707],
  Bengaluru:[12.9716,77.5946],
  Mumbai:[19.076,72.8777],
  Hyderabad:[17.3850,78.4867],
  'New Delhi':[28.6139,77.2090],
  Delhi:[28.6139,77.2090]
};
const cityMapCenter=city=>{
  const match=Object.keys(CITY_MAP_CENTERS).find(x=>x.toLowerCase()===String(city||'').trim().toLowerCase());
  return match?CITY_MAP_CENTERS[match]:INDIA_MAP_CENTER;
};


function PublicHome({authMode,setAuthMode,authForm,setAuthForm,doAuth,error,setError}){
 const[showAuth,setShowAuth]=useState(false);
 const[showLocationExplorer,setShowLocationExplorer]=useState(false);
 const[publicCities,setPublicCities]=useState([]);
 const[publicCity,setPublicCity]=useState('');
 const[publicPin,setPublicPin]=useState(null);
 const[nearbyOutlets,setNearbyOutlets]=useState([]);
 const[publicMapBusy,setPublicMapBusy]=useState(false);
 const[publicMapError,setPublicMapError]=useState('');
 const[trackingLocation,setTrackingLocation]=useState(false);
 const[publicOutlet,setPublicOutlet]=useState(null);
 const[publicOutletMenu,setPublicOutletMenu]=useState([]);
 const[publicOutletBusy,setPublicOutletBusy]=useState(false);
 const[publicOutletError,setPublicOutletError]=useState('');
 const[location,setLocation]=useState('');
 const[locationHint,setLocationHint]=useState('');
 const[guestBuilderOpen,setGuestBuilderOpen]=useState(false);
 const[guestBuilderOutlet,setGuestBuilderOutlet]=useState(null);
 const[guestDuration,setGuestDuration]=useState('OneWeek');
 const[guestStartDate,setGuestStartDate]=useState(todayISO());
 const[guestSelections,setGuestSelections]=useState({});
 const trackingRef=useRef(null);
 const lastTrackedRef=useRef(null);

 useEffect(()=>{
   let disposed=false;
   locations.cities().then(rows=>{
     if(disposed)return;
     const list=rows||[];
     setPublicCities(list);
     if(!publicCity)setPublicCity(list[0]?.city||'');
   }).catch(()=>{});
   return()=>{disposed=true};
 },[]);

 const loadNearbyOutlets=async(latitude,longitude,city)=>{
   if(!Number.isFinite(latitude)||!Number.isFinite(longitude))return;
   setPublicPin([latitude,longitude]);
   setPublicMapBusy(true);
   setPublicMapError('');
   try{
     const result=await outlets.availability(latitude,longitude,city||'');
     setNearbyOutlets(result?.outlets||[]);
   }catch(e){
     setNearbyOutlets([]);
     setPublicMapError(e.message||'Unable to find nearby outlets.');
   }finally{setPublicMapBusy(false)}
 };

 const openLocationExplorer=async()=>{
   setShowLocationExplorer(true);
   setPublicMapError('');
   if(!publicPin){
     setPublicMapBusy(true);
     try{setNearbyOutlets(await outlets.list(publicCity||undefined)||[]);}
     catch(e){setPublicMapError(e.message||'Unable to load outlets.');}
     finally{setPublicMapBusy(false);}
   }
 };
 const closeLocationExplorer=()=>{
   setShowLocationExplorer(false);
   if(trackingRef.current&&navigator.geolocation)navigator.geolocation.clearWatch(trackingRef.current);
   trackingRef.current=null;
   setTrackingLocation(false);
 };
 const useCurrentLocation=()=>{
   if(!navigator.geolocation){setPublicMapError('Location services are not available in this browser.');return;}
   setTrackingLocation(true);
   setPublicMapError('');
   const onSuccess=pos=>{
     const{latitude,longitude}=pos.coords;
     const last=lastTrackedRef.current;
     if(last){
       const distance=Math.sqrt(Math.pow((latitude-last[0])*111,2)+Math.pow((longitude-last[1])*111,2));
       if(distance<0.08)return;
     }
     lastTrackedRef.current=[latitude,longitude];
     loadNearbyOutlets(latitude,longitude,publicCity);
   };
   const onError=()=>{setTrackingLocation(false);setPublicMapError('Unable to access your current location. You can pin a location on the map instead.');};
   try{
     trackingRef.current=navigator.geolocation.watchPosition(onSuccess,onError,{enableHighAccuracy:true,maximumAge:10000,timeout:15000});
   }catch{
     navigator.geolocation.getCurrentPosition(onSuccess,onError,{enableHighAccuracy:true,timeout:15000});
   }
 };
 const changePublicCity=async city=>{
   setPublicCity(city);
   setPublicPin(null);
   lastTrackedRef.current=null;
   setPublicMapBusy(true);
   setPublicMapError('');
   try{setNearbyOutlets(await outlets.list(city)||[]);}
   catch(e){setNearbyOutlets([]);setPublicMapError(e.message||'Unable to load outlets for this city.');}
   finally{setPublicMapBusy(false);}
 };
 const selectPublicPin=(latitude,longitude)=>loadNearbyOutlets(latitude,longitude,publicCity);
 const startWithLocation=()=>{
   const value=location.trim();
   setLocationHint(value?("We'll use "+value+" to find outlets that can deliver to you."):"Pin your location to see nearby outlets and delivery coverage.");
   openLocationExplorer();
 };
 const publicBuilderDays=()=>{
   const d=DURATIONS.find(x=>x.id===guestDuration)||DURATIONS[2];
   const start=guestStartDate||todayISO();
   return Array.from({length:d.days},(_,i)=>({date:addDays(start,i),index:i}));
 };
 const publicMenuFor=(date,slot)=>{
   const day=dayId(date);
   return (publicOutletMenu||[]).filter(x=>Number(x.mealSlotValue)===Number(slot)&&Number(x.dayOfWeek)===Number(day));
 };
 const openGuestBuilder=()=>{
   if(!publicOutlet)return;
   setGuestBuilderOutlet(publicOutlet);
   const days=publicBuilderDays();
   const first={};
   for(const d of days){
     for(const s of SLOT){
       const opts=publicMenuFor(d.date,s.id);
       if(opts.length&&guestSelections[key(d.date,s.id)]===undefined) first[key(d.date,s.id)]='';
     }
   }
   setGuestSelections(g=>({...first,...g}));
   setGuestBuilderOpen(true);
   setPublicOutlet(null);
 };
 const guestMealRows=Object.values(guestSelections).filter(Boolean);
 const guestSelectedCount=guestMealRows.length;
 const saveGuestDraftAndCreateAccount=()=>{
   if(!guestBuilderOutlet||!guestSelectedCount)return;
   try{
     sessionStorage.setItem('healthapp.guestPackageDraft',JSON.stringify({
       outlet:guestBuilderOutlet,
       duration:guestDuration,
       startDate:guestStartDate||todayISO(),
       selections:Object.entries(guestSelections).filter(([,recipeId])=>recipeId).map(([k,recipeId])=>{
         const parts=k.split('_'); return {date:parts[0],slot:Number(parts[1]),recipeId,portion:1};
       })
     }));
   }catch{}
   setGuestBuilderOpen(false);
   setGuestBuilderOutlet(null);
   setPublicOutlet(null);
   setLocationHint('Your package is ready. Create an account to add delivery details, allergy preferences and continue to payment.');
   openAuth('register');
 };

 const openPublicOutlet=async o=>{
   setPublicOutlet(o);setPublicOutletError('');setPublicOutletBusy(true);
   try{
     const[m]=await Promise.all([menu.outlet(o.id)]);
     setPublicOutletMenu(m||[]);
   }catch(e){setPublicOutletMenu([]);setPublicOutletError(e.message||'Unable to load this outlet menu.');}
   finally{setPublicOutletBusy(false);}
 };
 const openAuth=mode=>{setAuthMode(mode);setShowAuth(true);setError('');window.scrollTo({top:0,behavior:'smooth'});};
 const goTo=id=>document.getElementById(id)?.scrollIntoView({behavior:'smooth',block:'start'});
 const plans=[
  {title:'Healthy Weekly',copy:'Balanced meals for the week with flexible meal choices.',badge:'Balanced nutrition',image:IMAGE_FALLBACKS.veg},
  {title:'Performance',copy:'Higher-protein meals designed for strength and active routines.',badge:'High protein',image:IMAGE_FALLBACKS.nonveg},
  {title:'Monthly Wellness',copy:'Wholesome everyday meals with convenient scheduled delivery.',badge:'Everyday wellness',image:IMAGE_FALLBACKS.hero},
  {title:'Plant Powered',copy:'Fresh vegetarian and vegan-friendly meals packed with variety.',badge:'Plant forward',image:IMAGE_FALLBACKS.vegan}
 ];
 const outletsFeatured=[
  {name:'FitFood Kitchen',city:'Bengaluru',copy:'Healthy · Fresh · Tasty',image:IMAGE_FALLBACKS.veg},
  {name:'Andhra Ruchulu',city:'Chennai',copy:'Regional · Fresh · Balanced',image:IMAGE_FALLBACKS.hero},
  {name:'Hyderabad Zaika',city:'Hyderabad',copy:'Deccan · Slow cooked · Fresh',image:IMAGE_FALLBACKS.nonveg},
  {name:'Deccan Wok',city:'Hyderabad',copy:'Wok tossed · Fast · Fresh',image:IMAGE_FALLBACKS.vegan}
 ];
 const steps=[
  ['1','📍','Set your location','Tell us where you want your meals delivered.'],
  ['2','🏪','Select an outlet','Choose a healthy meal outlet that serves your location.'],
  ['3','🍱','Explore meal plans','Browse meals, nutrition, ingredients and available plans.'],
  ['4','📅','Select & subscribe','Choose your schedule, meals, portions and subscribe.'],
  ['5','🚚','Outlet delivers','Your selected outlet prepares and delivers your meals.']
 ];

 if(showAuth) return <div className="publicAuthShell">
   <button className="publicBackBtn" type="button" onClick={()=>setShowAuth(false)}>← Back to HealthApp</button>
   <div className="publicAuthIntro"><span className="eyebrow">CUSTOMER PORTAL</span><h1>{authMode==='login'?'Welcome back':'Start your healthy journey'}</h1><p>{authMode==='login'?'Sign in to manage your meals and deliveries.':'Create your account to explore healthy outlets, meal plans and subscriptions.'}</p>{locationHint&&<div className="publicLocationNote">📍 {locationHint}</div>}</div>
   <form className="authCard publicAuthCard" onSubmit={doAuth}><div className="eyebrow">{authMode==='login'?'SIGN IN':'CREATE YOUR ACCOUNT'}</div><h2>{authMode==='login'?'Welcome back':'Create your account'}</h2><p>{authMode==='login'?'Sign in to manage your meals and deliveries.':'Start with your health profile and build your first package.'}</p>{authMode==='register'&&<div className="twoCol"><label>First name<input value={authForm.firstName} onChange={e=>setAuthForm({...authForm,firstName:e.target.value})}/></label><label>Last name<input value={authForm.lastName} onChange={e=>setAuthForm({...authForm,lastName:e.target.value})}/></label></div>}<label>Email<input type="email" value={authForm.email} onChange={e=>setAuthForm({...authForm,email:e.target.value})}/></label><label>Password<input type="password" value={authForm.password} onChange={e=>setAuthForm({...authForm,password:e.target.value})}/></label><button className="primary big">{authMode==='login'?'Sign in':'Create account'}</button>{error&&<div className="error">{error}</div>}<div className="authSwitch">{authMode==='login'?'New to HealthApp?':'Already have an account?'} <button type="button" className="linkBtn" onClick={()=>setAuthMode(authMode==='login'?'register':'login')}>{authMode==='login'?'Create account':'Sign in'}</button></div>{authMode==='login'&&<small>Demo: customer@healthapp.test / demo</small>}</form>
 </div>;

 return <div className="publicHome">
   <header className="publicNav">
     <button className="publicBrand" type="button" onClick={()=>goTo('public-top')}><span className="brandMark">H</span><span><b>HealthApp</b><small>Healthy meals, built around you</small></span></button>
     <nav className="publicNavLinks"><button onClick={()=>goTo('how-it-works')}>How it works</button><button onClick={()=>goTo('plans')}>Meal Plans</button><button onClick={()=>goTo('outlets')}>Our Outlets</button><button onClick={()=>goTo('why-healthapp')}>Why HealthApp</button></nav>
     <div className="publicNavActions"><button className="secondary smallBtn" onClick={()=>openAuth('login')}>Sign in</button><button className="primary smallBtn" onClick={()=>openAuth('register')}>Create account</button></div>
   </header>
   <main id="public-top">
    <section className="publicHero">
      <div className="publicHeroImage"><img src={IMAGE_FALLBACKS.hero} alt="Healthy meal bowl"/><div className="publicHeroCallout"><b>Good food.<br/>Better days.</b><span>Nutritious meals from local outlets</span></div></div>
      <div className="publicHeroCopy">
        <span className="publicEyebrow">HEALTHY MEAL SUBSCRIPTION</span><h1>Healthy Meals.<br/>Happier You.</h1>
        <p>Discover healthy meal subscriptions from trusted local outlets. Choose where you want delivery, explore plans and let your selected outlet do the rest.</p>
        <div className="publicLocationBar"><span>⌖</span><input value={location} onChange={e=>setLocation(e.target.value)} placeholder="Enter your delivery location"/><button className="primary" onClick={startWithLocation}>Find Meals →</button></div>
        <div className="publicHeroBadges"><span>✓ Healthy & balanced</span><span>✓ Trusted local outlets</span><span>✓ Flexible subscriptions</span><span>✓ Freshly prepared & delivered</span></div>
      </div>
    </section>

    <section className="publicSection publicHow" id="how-it-works">
      <div className="publicSectionHead"><span className="publicEyebrow">SIMPLE FROM START TO FINISH</span><h2>How it works</h2><p>Five simple steps from choosing your location to receiving your meals.</p></div>
      <div className="publicSteps">{steps.map(([n,icon,title,copy],idx)=><div className={"publicStep "+(n==='1'?'publicStepLive':'')} key={n}><button className="publicStepInteractive" type="button" onClick={()=>n==='1'&&openLocationExplorer()}><div className="publicStepTop"><span>{n}</span>{idx<steps.length-1&&<i>→</i>}</div><div className="publicStepIcon">{icon}</div><h3>{title}</h3><p>{copy}</p></button>{n==='1'&&<small className="publicStepLiveHint">Live outlet availability</small>}</div>)}</div>
    </section>

    <section className="publicBenefits" id="why-healthapp">
      <div><b>♥</b><strong>Healthy & Nutritious</strong><span>Meals built around better everyday choices.</span></div>
      <div><b>◉</b><strong>Local Trusted Outlets</strong><span>Choose outlets serving healthy options near you.</span></div>
      <div><b>↔</b><strong>Flexible Plans</strong><span>Daily, weekly or monthly subscription options.</span></div>
      <div><b>⌖</b><strong>Convenient Delivery</strong><span>Meals delivered to home or office on schedule.</span></div>
    </section>

    <section className="publicSection" id="plans">
      <div className="publicSectionHead inline"><div><span className="publicEyebrow">POPULAR OPTIONS</span><h2>Explore meal plans</h2><p>Choose a subscription style that fits your goals and routine.</p></div><button className="linkBtn" onClick={()=>openAuth('register')}>View all plans →</button></div>
      <div className="publicPlanGrid">{plans.map(p=><article className="publicPlanCard" key={p.title}><img src={p.image} alt=""/><div><span className="publicPlanBadge">✓ {p.badge}</span><h3>{p.title}</h3><p>{p.copy}</p><button className="secondary smallBtn" onClick={()=>openAuth('register')}>Explore plan →</button></div></article>)}</div>
    </section>

    <section className="publicSection publicOutletsSection" id="outlets">
      <div className="publicSectionHead inline"><div><span className="publicEyebrow">LOCAL PARTNERS</span><h2>Our featured outlets</h2><p>Healthy meal options from outlets serving supported cities.</p></div><button className="linkBtn" onClick={openLocationExplorer}>View all outlets →</button></div>
      <div className="publicOutletGrid">{outletsFeatured.map(o=><article className="publicOutletCard" key={o.name}><img src={o.image} alt=""/><div><b>{o.name}</b><span>{o.city}</span><small>{o.copy}</small></div></article>)}</div>
    </section>

    <section className="publicStory"><div className="publicStoryImage"><img src={IMAGE_FALLBACKS.nonveg} alt="Prepared healthy meal"/></div><div><span className="publicEyebrow">BUILT FOR EVERYDAY LIFE</span><h2>One place to discover, subscribe and manage healthy meals.</h2><p>Set your delivery location, choose an outlet, build a package around your preferred meals and manage addresses, meal calendars, skips and credits from one customer account.</p><button className="primary" onClick={()=>openAuth('register')}>Create account →</button></div></section>

    <section className="publicCta"><div><span className="publicEyebrow">READY TO GET STARTED?</span><h2>Find healthy meals that fit your life.</h2><p>Create an account and start exploring outlets and meal subscriptions in your supported city.</p></div><button className="primary big" onClick={()=>openAuth('register')}>Create account</button></section>
   </main>

   {showLocationExplorer&&<div className="publicOverlayBackdrop" onMouseDown={e=>e.target===e.currentTarget&&closeLocationExplorer()}>
     <div className="publicLocationExplorer">
       <div className="publicExplorerHead"><div><span className="publicEyebrow">LIVE OUTLET DISCOVERY</span><h2>Find healthy outlets near you</h2><p>Move the map or use your current location to see outlets that can deliver to you.</p></div><button className="publicExplorerClose" onClick={closeLocationExplorer}>×</button></div>
       <div className="publicExplorerToolbar"><label><span>Delivery city</span><select value={publicCity} onChange={e=>changePublicCity(e.target.value)}>{publicCities.map(x=><option key={x.city+'|'+x.state} value={x.city}>{x.city} · {x.state}</option>)}</select></label><button className="secondary smallBtn" onClick={useCurrentLocation}>{trackingLocation?'● Live location on':'⌖ Use my current location'}</button>{publicPin&&<span className="publicPinStatus">● Pin updated · {nearbyOutlets.length} outlet{nearbyOutlets.length===1?'':'s'} nearby</span>}</div>
       {publicMapError&&<div className="publicExplorerError">{publicMapError}</div>}
       <div className="publicExplorerGrid">
         <div className="publicExplorerMap"><MapContainer center={publicPin||cityMapCenter(publicCity)} zoom={13} scrollWheelZoom className="publicLiveMap"><TileLayer url={MAP_TILE_URL} attribution={MAP_ATTRIBUTION}/><MapRecenter center={publicPin||cityMapCenter(publicCity)}/><MapClickHandler onPick={selectPublicPin}/>{nearbyOutlets.map(o=>Number.isFinite(Number(o.latitude))&&Number.isFinite(Number(o.longitude))&&<CircleMarker key={o.id} center={[Number(o.latitude),Number(o.longitude)]} radius={9} pathOptions={{fillOpacity:.9}} eventHandlers={{click:()=>openPublicOutlet(o)}}/>)}{publicPin&&<CircleMarker center={publicPin} radius={10} pathOptions={{weight:3,fillOpacity:.2}}/>}</MapContainer>{publicMapBusy&&<div className="publicMapLoading">Finding outlets…</div>}<div className="publicMapHint">Click anywhere on the map to check outlet coverage at that location.</div></div>
         <aside className="publicNearbyPanel"><div className="publicNearbyHead"><div><b>{nearbyOutlets.length?nearbyOutlets.length+' outlets nearby':'Nearby outlets'}</b><span>{publicCity||'Choose a city'}</span></div><span>LIVE</span></div>{nearbyOutlets.length?nearbyOutlets.map(o=><article className="publicNearbyOutlet" key={o.id} onClick={()=>openPublicOutlet(o)}><img src={o.logoUrl?getImg(o.logoUrl):IMAGE_FALLBACKS.logo} alt="" onError={e=>e.currentTarget.src=IMAGE_FALLBACKS.logo}/><div><b>{o.name}</b><small>★ {Number(o.rating||4.8).toFixed(1)} · {o.distanceKm?o.distanceKm+' km':'Nearby'}</small><span>{(o.healthHighlights||[]).slice(0,2).join(' · ')}</span></div><strong>→</strong></article>):<div className="publicNearbyEmpty"><div>⌖</div><b>Set a location to discover outlets</b><span>Use your current location or click a point on the map.</span></div>}</aside>
       </div>
       {locationHint&&<div className="publicLocationExplorerNote">📍 {locationHint}</div>}
     </div>
   </div>}


   {guestBuilderOpen&&<div className="publicOverlayBackdrop" onMouseDown={e=>e.target===e.currentTarget&&setGuestBuilderOpen(false)}>
     <div className="publicGuestBuilder">
       <div className="publicExplorerHead">
         <div><span className="publicEyebrow">GUEST PACKAGE BUILDER</span><h2>Build your package</h2><p>Choose your meals first. We'll ask you to create an account when you're ready to continue.</p></div>
         <button className="publicExplorerClose" onClick={()=>setGuestBuilderOpen(false)}>×</button>
       </div>
       <div className="publicGuestBuilderToolbar">
         <label><span>Package duration</span><select value={guestDuration} onChange={e=>{setGuestDuration(e.target.value);setGuestSelections({})}}>{DURATIONS.map(d=><option key={d.id} value={d.id}>{d.label}</option>)}</select></label>
         <label><span>Start date</span><input type="date" min={todayISO()} value={guestStartDate} onChange={e=>{setGuestStartDate(e.target.value);setGuestSelections({})}}/></label>
         <div className="publicGuestCount"><b>{guestSelectedCount}</b><span>meals selected</span></div>
         <div className="publicGuestOutlet"><span>OUTLET</span><b>{guestBuilderOutlet?.name}</b><small>{guestBuilderOutlet?.city}</small></div>
       </div></div>
       <div className="publicGuestWeeks">
         {publicBuilderDays().map(d=><section className="publicGuestDay" key={d.date}>
           <div className="publicGuestDayHead"><div><b>{dayName(dayId(d.date))}</b><span>{shortDate(d.date)}</span></div><small>{SLOT.filter(s=>publicMenuFor(d.date,s.id).length).length} meal slots available</small></div>
           <div className="publicGuestSlots">
             {SLOT.map(s=>{
               const opts=publicMenuFor(d.date,s.id);
               if(!opts.length)return null;
               const selected=guestSelections[key(d.date,s.id)]||'';
               return <label key={s.id}><span>{s.icon} {s.label}</span><select value={selected} onChange={e=>setGuestSelections(g=>({...g,[key(d.date,s.id)]:e.target.value}))}><option value="">Choose a meal</option>{opts.map(m=><option key={m.recipeId} value={m.recipeId}>{m.recipeName} · {money(m.pricePerMeal)}</option>)}</select></label>;
             })}
           </div>
           {!SLOT.some(s=>publicMenuFor(d.date,s.id).length)&&<div className="publicGuestNoMenu">No menu is published for this day.</div>}
         </section>)}
       </div>
       <div className="publicGuestFooter"><div><b>{guestSelectedCount} meals selected</b><span>After creating your account, we'll ask for your exact delivery address and allergy preferences before payment.</span></div><button className="primary big" disabled={!guestSelectedCount} onClick={saveGuestDraftAndCreateAccount}>Create account to continue →</button></div>
     </div>
   </div>}

   {publicOutlet&&<div className="publicOverlayBackdrop" onMouseDown={e=>e.target===e.currentTarget&&setPublicOutlet(null)}>
     <div className="publicOutletPreview">
       <div className="publicPreviewHero"><img src={publicOutlet.heroImageUrl?getImg(publicOutlet.heroImageUrl):IMAGE_FALLBACKS.hero} alt="" onError={e=>e.currentTarget.src=IMAGE_FALLBACKS.hero}/><div><span className="publicEyebrow">HEALTHY LOCAL OUTLET</span><h2>{publicOutlet.name}</h2><p>{publicOutlet.city}, {publicOutlet.state} · ★ {Number(publicOutlet.rating||4.8).toFixed(1)} ({publicOutlet.reviewCount||0})</p></div><button className="publicExplorerClose" onClick={()=>setPublicOutlet(null)}>×</button></div>
       <div className="publicPreviewBody">{publicOutletBusy?<div className="publicNearbyEmpty">Loading outlet menu…</div>:publicOutletError?<div className="publicExplorerError">{publicOutletError}</div>:<><div className="publicPreviewIntro"><div><b>Explore the menu before creating an account</b><span>Browse meals and nutrition from this outlet as a guest.</span></div><button className="primary" onClick={openGuestBuilder}>Build Package →</button></div><div className="publicPreviewMeals">{publicOutletMenu.slice(0,6).map(m=><article key={m.id}><img src={m.imageUrl?getImg(m.imageUrl):IMAGE_FALLBACKS.veg} alt="" onError={e=>e.currentTarget.src=IMAGE_FALLBACKS.veg}/><div><b>{m.recipeName}</b><span>{m.calories} kcal · {m.proteinGrams}g protein</span><small>{m.category} · {money(m.pricePerMeal)}</small></div></article>)}</div>{!publicOutletMenu.length&&<div className="publicNearbyEmpty"><b>No menu published yet</b><span>This outlet has not published meals for guest browsing.</span></div>}</>}</div>
     </div>
   </div>}
 </div>;
}

function App(){
 const[user,setUser]=useState(currentUser());
 const[active,setActive]=useState('dashboard');
 const[loading,setLoading]=useState(false);
 const[error,setError]=useState('');
 const[toast,setToast]=useState('');
 const[toastType,setToastType]=useState('success');
 const[customerDashboard,setCustomerDashboard]=useState(null);
 const[mobileMenuOpen,setMobileMenuOpen]=useState(false);
 const[global,setGlobal]=useState({outlets:[],areas:[],cities:[],addresses:[],profile:null,allergens:[],subscriptions:[],orders:[],credit:{balance:0},transactions:[],likedMeals:[]});
 const[selectedOutlet,setSelectedOutlet]=useState(null);
 const[outletMenu,setOutletMenu]=useState([]);
 const[outletRecipes,setOutletRecipes]=useState([]);
 const[outletCategory,setOutletCategory]=useState('All');
 const[recipeView,setRecipeView]=useState(null);
 const[selectedAddressId,setSelectedAddressId]=useState('');
 const[pendingBuilderOutlet,setPendingBuilderOutlet]=useState(null);
 const[authMode,setAuthMode]=useState('login');
 const[authForm,setAuthForm]=useState({email:'customer@healthapp.test',password:'demo',firstName:'Demo',lastName:'Customer'});
 const[profileForm,setProfileForm]=useState({weightKg:'',heightCm:'',dateOfBirth:'',goal:'WeightLoss',activityLevel:'Moderate',allergyIds:[],diet:'Veg'});
 const[addressModal,setAddressModal]=useState(null);
 const[mapBusy,setMapBusy]=useState(false);
 const[addressForm,setAddressForm]=useState({id:null,city:'',pincode:'',locality:'',cityAreaId:null,label:'Home',addressLine1:'',addressLine2:'',contactName:'',contactPhone:'',latitude:'',longitude:'',isDefault:false});
 const[builder,setBuilder]=useState({outlet:null,deliveryCity:'',duration:'OneWeek',deliveryMode:'OneDeliveryPerDay',startDate:nextMonday(),weeks:1,weekActiveDays:blankWeeks,selections:{},allergyAcknowledged:{},dayAddresses:{},discountCode:'',quote:null,step:1});
 const[picker,setPicker]=useState(null);
 const[subs,setSubs]=useState([]);
 const[selectedSubId,setSelectedSubId]=useState('');
 const[calendarWeek,setCalendarWeek]=useState(nextMonday());
 const[mealSelections,setMealSelections]=useState([]);
 const[reschedule,setReschedule]=useState(null);
 const[paymentSubId,setPaymentSubId]=useState('');
 const[cityFilter,setCityFilter]=useState('');
 const[availableOutlets,setAvailableOutlets]=useState(null);
 const[guestPackageReady,setGuestPackageReady]=useState(false);
 const[guestPackageRestored,setGuestPackageRestored]=useState(false);

 const notify=(m,type='success')=>{setToast(m);setToastType(type);setTimeout(()=>setToast(''),2600)};
 const run=async(fn)=>{setLoading(true);setError('');try{return await fn()}catch(e){setError(e.message||'Something went wrong');throw e}finally{setLoading(false)}};
 const reload=async()=>run(async()=>{
   const[cities,os,ads,p,allergens,ss,orders,credit,transactions,dashboard,likedMeals]=await Promise.all([locations.cities(),outlets.list(cityFilter),customer.addresses(),customer.profile(),catalog.allergens(),customer.subscriptions(),customer.orders(),customer.credit(),customer.dashboard(),customer.likedMeals()]);
   setGlobal({outlets:os,areas:[],cities,addresses:ads,profile:p,allergens,subscriptions:ss,orders,credit,transactions,likedMeals:likedMeals||[]});setSubs(ss);setCustomerDashboard(dashboard);
   const initialCity=cityFilter||(ads.find(x=>x.isDefault)?.city)||cities[0]?.city||'';if(!cityFilter&&initialCity)setCityFilter(initialCity);
   const cityAddresses=ads.filter(x=>x.city?.toLowerCase()===initialCity.toLowerCase());setSelectedAddressId((cityAddresses.find(x=>x.isDefault)||cityAddresses[0])?.id||'');
   if(p)setProfileForm({weightKg:p.weightKg??'',heightCm:p.heightCm??'',dateOfBirth:p.dateOfBirth?.slice?.(0,10)||'',goal:p.goal||'WeightLoss',activityLevel:p.activityLevel||'Moderate',allergyIds:(p.allergies||[]).map(a=>a.id),diet:p.diet||'Veg'});
 });
 useEffect(()=>{
   if(!user||guestPackageRestored)return;
   let raw=null;
   try{raw=localStorage.getItem('healthapp.savedPackageDraft')||sessionStorage.getItem('healthapp.guestPackageDraft')}catch{}
   if(!raw)return;
   let draft=null;
   try{draft=JSON.parse(raw)}catch{}
   if(!draft||!draft.outlet?.id||!Array.isArray(draft.selections)||!draft.selections.length)return;
   const currentId=currentUser()?.id;
   if(draft.customerId&&currentId&&draft.customerId!==currentId)return;
   setGuestPackageRestored(true);
   restoreSavedPackage(draft).catch(()=>{});
 },[user,guestPackageRestored]);
 useEffect(()=>{if(user)reload().catch(()=>{})},[user]);
 useEffect(()=>{if(!user||!cityFilter)return;let disposed=false;outlets.list(cityFilter).then(outletsForCity=>{if(disposed)return;setGlobal(g=>({...g,outlets:outletsForCity}));setAvailableOutlets(outletsForCity)}).catch(()=>{});return()=>{disposed=true}},[cityFilter,user]);
 useEffect(()=>{if(!user)return;const city=cityFilter.toLowerCase();const cityAddresses=global.addresses.filter(a=>a.city?.toLowerCase()===city);setSelectedAddressId(prev=>{const current=global.addresses.find(x=>x.id===prev);if(current?.city?.toLowerCase()===city)return prev;return(cityAddresses.find(x=>x.isDefault)||cityAddresses[0])?.id||''})},[cityFilter,global.addresses,user]);
 useEffect(()=>{
   if(!user||active!=='dashboard')return;
   let disposed=false;
   const refresh=()=>customer.dashboard().then(x=>{if(!disposed)setCustomerDashboard(x)}).catch(()=>{});
   refresh();
   const timer=setInterval(refresh,30000);
   window.addEventListener('focus',refresh);
   return()=>{disposed=true;clearInterval(timer);window.removeEventListener('focus',refresh)};
 },[user,active]);
 useEffect(()=>{if(!selectedAddressId){setAvailableOutlets(global.outlets);return}const a=global.addresses.find(x=>x.id===selectedAddressId);if(!a||a.city?.toLowerCase()!==cityFilter.toLowerCase()){setAvailableOutlets(global.outlets);return}outlets.availability(a.latitude,a.longitude,cityFilter).then(x=>setAvailableOutlets(x.outlets||[])).catch(()=>setAvailableOutlets(global.outlets))},[selectedAddressId,global.addresses,global.outlets,cityFilter]);

 const restoreSavedPackage=async draft=>{
   if(!draft?.outlet?.id||!Array.isArray(draft.selections)||!draft.selections.length)return false;
   const d=DURATIONS.find(x=>x.id===draft.duration)||DURATIONS[2];
   const start=draft.startDate||nextMonday();
   try{
     setLoading(true);
     const[m,rs]=await Promise.all([menu.outlet(draft.outlet.id),recipes.list(draft.outlet.id)]);
     const selections={};
     draft.selections.forEach(x=>{
       if(x?.date&&x?.slot&&x?.recipeId)selections[key(x.date,Number(x.slot))]={date:x.date,slot:Number(x.slot),recipeId:x.recipeId,portion:Number(x.portion||1)};
     });
     const activeDays=Array.from(new Set(draft.selections.map(x=>dayId(x.date)).filter(Number.isInteger)));
     setSelectedOutlet(draft.outlet);
     setOutletMenu(m||[]);
     setOutletRecipes(rs||[]);
     setOutletCategory('All');
     setBuilder({
       outlet:draft.outlet,
       deliveryCity:draft.outlet.city||'',
       duration:draft.duration||'OneWeek',
       deliveryMode:draft.deliveryMode||'OneDeliveryPerDay',
       startDate:start,
       weeks:d.weeks||d.weeks||1,
       weekActiveDays:{1:activeDays.length?activeDays:[1,2,3,4,5]},
       selections,
       allergyAcknowledged:{},
       dayAddresses:{},
       discountCode:draft.discountCode||'',
       quote:null,
       step:1
     });
     setGuestPackageReady(true);
     setActive('builder');
     try{localStorage.setItem('healthapp.savedPackageDraft',JSON.stringify({...draft,customerId:currentUser()?.id||null}));sessionStorage.removeItem('healthapp.guestPackageDraft')}catch{}
     notify('Your saved package is ready. Complete delivery and allergy details before payment.','info');
     return true;
   }catch(e){
     setError(e.message||'Unable to restore your saved package.');
     return false;
   }finally{setLoading(false)}
 };

 const doAuth=async e=>{e.preventDefault();await run(async()=>{
   const x=authMode==='login'
     ?await auth.login({email:authForm.email,password:authForm.password})
     :await auth.register({firstName:authForm.firstName,lastName:authForm.lastName,email:authForm.email,password:authForm.password,role:'Customer'});
   setUser(x.user);
   const draftRaw=(()=>{try{return sessionStorage.getItem('healthapp.guestPackageDraft')||localStorage.getItem('healthapp.savedPackageDraft')}catch{return null}})();
   const draft=draftRaw?(()=>{try{return JSON.parse(draftRaw)}catch{return null}})():null;
   if(draft){await restoreSavedPackage(draft);setGuestPackageRestored(true);}
   else{setActive('dashboard');notify(authMode==='login'?'Welcome back':'Account created');}
 })};
 const logout=()=>{auth.logout();setUser(null);setMobileMenuOpen(false)};
 const go=tab=>{setActive(tab);setMobileMenuOpen(false)};
 const toggleLikedMeal=async recipeId=>{
   const liked=global.likedMeals.some(x=>x.recipeId===recipeId);
   try{
     if(liked){await customer.unlikeMeal(recipeId);setGlobal(g=>({...g,likedMeals:g.likedMeals.filter(x=>x.recipeId!==recipeId)}));notify('Removed from liked meals','info');}
     else{const meal=await customer.likeMeal(recipeId);setGlobal(g=>({...g,likedMeals:[meal,...g.likedMeals.filter(x=>x.recipeId!==recipeId)]}));notify('Added to liked meals');}
   }catch(e){setError(e.message||'Unable to update liked meals.')}
 };

 const changeDiscoveryCity=city=>{setCityFilter(city);setSelectedOutlet(null);setOutletMenu([]);setOutletRecipes([]);setOutletCategory('All');setSelectedAddressId('')};
 const openOutlet=async o=>{setSelectedOutlet(o);setOutletMenu([]);setOutletRecipes([]);setOutletCategory('All');setActive('discover');setError('');try{setLoading(true);const[m,rs]=await Promise.all([menu.outlet(o.id),recipes.list(o.id)]);setOutletMenu(m||[]);setOutletRecipes(rs||[])}catch(e){setError(e.message||'Unable to load outlet menu.')}finally{setLoading(false)}};
 const openAddressForCity=async(city,reason=true)=>{setCityFilter(city);setAddressModal('new');setAddressForm({id:null,city,pincode:'',locality:'',cityAreaId:null,label:'Home',addressLine1:'',addressLine2:'',contactName:(user.firstName+' '+user.lastName).trim(),contactPhone:'',latitude:'',longitude:'',isDefault:global.addresses.length===0});if(reason)notify('Set the exact delivery pin anywhere in '+city+'. We will check outlet availability from this location.','info')};
 const startBuilder=async(o,preferredAddress=null)=>{const city=o.city||'';const cityAddress=preferredAddress?.id?preferredAddress:(selectedAddressId?global.addresses.find(a=>a.id===selectedAddressId&&a.city?.toLowerCase()===city.toLowerCase()):null)||global.addresses.find(a=>a.city?.toLowerCase()===city.toLowerCase()&&a.isDefault)||global.addresses.find(a=>a.city?.toLowerCase()===city.toLowerCase());if(!cityAddress){setPendingBuilderOutlet(o);await openAddressForCity(city,true);return}setPendingBuilderOutlet(null);const d=DURATIONS.find(x=>x.id==='OneWeek')||DURATIONS[2];const start=nextMonday();const dayAddresses={};for(let i=0;i<d.days;i++)dayAddresses[addDays(start,i)]=cityAddress.id;setSelectedOutlet(o);setError('');setActive('builder');try{setLoading(true);const[m,rs]=await Promise.all([menu.outlet(o.id),recipes.list(o.id)]);setOutletMenu(m||[]);setOutletRecipes(rs||[]);setBuilder({outlet:o,deliveryCity:city,duration:'OneWeek',deliveryMode:'OneDeliveryPerDay',startDate:start,weeks:d.weeks,weekActiveDays:blankWeeks,selections:{},allergyAcknowledged:{},dayAddresses,discountCode:'',quote:null,step:1})}catch(e){setError(e.message||'Unable to load outlet menu.')}finally{setLoading(false)}};

 const filteredRecipes=useMemo(()=>outletCategory==='All'?outletRecipes:outletRecipes.filter(r=>r.category===outletCategory),[outletRecipes,outletCategory]);

 const saveProfile=async()=>run(async()=>{const x=await customer.saveProfile({...profileForm,weightKg:profileForm.weightKg===''?null:Number(profileForm.weightKg),heightCm:profileForm.heightCm===''?null:Number(profileForm.heightCm),dateOfBirth:profileForm.dateOfBirth||null,allergyIds:profileForm.allergyIds||[]});setGlobal(g=>({...g,profile:x}));setBuilder(b=>({...b,allergyAcknowledged:{},quote:null}));if(guestPackageReady)setActive('builder');notify(guestPackageReady?'Preferences saved. Review your package for any allergy warnings.':'Health profile saved')});
 const openNewAddress=()=>openAddressForCity(cityFilter,false);
 const editAddress=a=>{setAddressModal('edit');setAddressForm({id:a.id,city:a.city,pincode:a.pincode||'',locality:a.areaName||'',cityAreaId:null,label:a.label,addressLine1:a.addressLine1,addressLine2:a.addressLine2,contactName:a.contactName,contactPhone:a.contactPhone,latitude:a.latitude,longitude:a.longitude,isDefault:a.isDefault});setCityFilter(a.city)};
 const pickAddressLocation=async(latitude,longitude)=>{const requestedCity=(addressForm.city||cityFilter||'').trim();
 setError('');
 // Save the coordinates immediately; reverse geocoding only enriches editable address fields.
 setAddressForm(f=>({...f,latitude,longitude}));
 setMapBusy(true);
 try{
   const g=await locations.reverseGeocode(latitude,longitude);
   if(g?.city&&requestedCity&&g.city.localeCompare(requestedCity,undefined,{sensitivity:'base'})!==0)
     throw new Error('This pin is in '+g.city+', but the selected delivery city is '+requestedCity+'. Please place the pin inside '+requestedCity+'.');
   if(g?.city){
     setAddressForm(f=>({...f,city:g.city,pincode:g.pincode||f.pincode||'',locality:g.suburb||g.neighbourhood||f.locality||'',cityAreaId:null,addressLine1:[g.houseNumber,g.road].filter(Boolean).join(' ').trim()||f.addressLine1,addressLine2:g.suburb||g.neighbourhood||f.addressLine2,latitude,longitude}));
     notify('Exact pin set in '+g.city+'. Review the address details, then save.','info');
   }else{
     notify('Pin set. You can enter the address details manually.','info');
   }
 }catch(e){
   const message=e.message||'Unable to enrich this location.';
   setError(message);
   setAddressForm(f=>({...f,latitude,longitude}));
 }finally{setMapBusy(false)}};

 const saveAddress=async()=>run(async()=>{if(!addressForm.city.trim())throw new Error('Delivery city is required.');if(!addressForm.addressLine1.trim())throw new Error('Address line 1 is required.');const lat=Number(addressForm.latitude),lng=Number(addressForm.longitude);if(!Number.isFinite(lat)||!Number.isFinite(lng))throw new Error('Pick the exact delivery location on the map.');const payload={city:addressForm.city,pincode:addressForm.pincode||'',locality:addressForm.locality||'',label:addressForm.label,addressLine1:addressForm.addressLine1,addressLine2:addressForm.addressLine2,contactName:addressForm.contactName,contactPhone:addressForm.contactPhone,latitude:lat,longitude:lng,cityAreaId:null,isDefault:Boolean(addressForm.isDefault)};const x=addressModal==='new'?await customer.createAddress(payload):await customer.updateAddress(addressForm.id,payload);const list=addressModal==='new'?[...global.addresses,x]:global.addresses.map(a=>a.id===x.id?x:a);setGlobal(g=>({...g,addresses:list}));setSelectedAddressId(x.id);const nextBuilderOutlet=pendingBuilderOutlet;setPendingBuilderOutlet(null);setAddressModal(null);if(nextBuilderOutlet&&nextBuilderOutlet.city?.toLowerCase()===x.city?.toLowerCase()){await startBuilder(nextBuilderOutlet,x)}else{if(guestPackageReady||builder.outlet)setActive('builder');notify(guestPackageReady?'Delivery address saved. Review your package to continue.':'Address saved with exact map location')}});
 const deleteAddress=async a=>{if(!confirm('Delete this address?'))return;await run(async()=>{await customer.deleteAddress(a.id);const next=global.addresses.filter(x=>x.id!==a.id);setGlobal(g=>({...g,addresses:next}));setSelectedAddressId(next[0]?.id||'');notify('Address deleted')})};

 const setBuilderDuration=value=>{const d=DURATIONS.find(x=>x.id===value)||DURATIONS[0];setBuilder(b=>{const next={...b.weekActiveDays};for(let i=1;i<=d.weeks;i++)next[i]=next[i]||next[1]||[1,2,3,4,5];const city=(b.deliveryCity||b.outlet?.city||'').toLowerCase();const defaultAddress=global.addresses.find(a=>a.city?.toLowerCase()===city);const nextAddresses={...b.dayAddresses};for(let i=0;i<d.days;i++){const date=addDays(b.startDate,i);if(defaultAddress&&!nextAddresses[date])nextAddresses[date]=defaultAddress.id}return{...b,duration:value,weeks:d.weeks,weekActiveDays:next,dayAddresses:nextAddresses,quote:null}})};
 const builderDays=useMemo(()=>Array.from({length:builder.weeks*7},(_,i)=>({date:addDays(builder.startDate,i),index:i,week:Math.floor(i/7)+1})),[builder.startDate,builder.weeks]);
 const menuMap=useMemo(()=>{const m={};for(const x of outletMenu){const k=key(x.dayOfWeek,x.mealSlotValue);(m[k]??=[]).push(x)}return m},[outletMenu]);
 const isActiveDay=(week,date)=>Boolean(builder.weekActiveDays[week]?.includes(dayId(date)));
 const builderSelections=useMemo(()=>Object.values(builder.selections).filter(Boolean).filter(x=>{const d=builderDays.find(y=>y.date===x.date);return d?isActiveDay(d.week,x.date):true}),[builder.selections,builderDays,builder.weekActiveDays]);
 const selectedCount=builderSelections.length;
 const setSelection=(date,slot,recipeId,portion=1,allergyConfirmed=false)=>setBuilder(b=>({...b,selections:{...b.selections,[key(date,slot)]:recipeId?{date,slot,recipeId,portion}:undefined},allergyAcknowledged:allergyConfirmed&&recipeId?{...b.allergyAcknowledged,[recipeId]:true}:b.allergyAcknowledged,quote:null}));
 const setDayAddress=(date,id)=>setBuilder(b=>({...b,dayAddresses:{...b.dayAddresses,[date]:id},quote:null}));
 const setMealAddress=(date,slot,id)=>setBuilder(b=>({...b,dayAddresses:{...b.dayAddresses,[key(date,slot)]:id},quote:null}));
 const toggleDay=(week,id)=>setBuilder(b=>({...b,weekActiveDays:{...b.weekActiveDays,[week]:(b.weekActiveDays[week]||[]).includes(id)?(b.weekActiveDays[week]||[]).filter(x=>x!==id):[...(b.weekActiveDays[week]||[]),id]},quote:null}));
 const copyWeek=fromWeek=>setBuilder(b=>{const nextSel={...b.selections};const source=builderDays.filter(x=>x.week===fromWeek);for(let w=1;w<=b.weeks;w++){if(w===fromWeek)continue;for(const d of source){const target=builderDays.find(x=>x.week===w&&x.index%7===d.index%7);if(!target)continue;for(const s of SLOT){const v=nextSel[key(d.date,s.id)];nextSel[key(target.date,s.id)]=v?{...v,date:target.date}:undefined}}}const base=b.weekActiveDays[fromWeek]||[];const wa={...b.weekActiveDays};for(let w=1;w<=b.weeks;w++)if(w!==fromWeek)wa[w]=[...base];return{...b,selections:nextSel,weekActiveDays:wa,quote:null}});
 const selectionPayload=useMemo(()=>builderSelections.map(x=>({mealDate:x.date,mealSlot:x.slot,recipeId:x.recipeId,portionSize:x.portion,addressId:builder.deliveryMode==='OneDeliveryPerDay'?(builder.dayAddresses[x.date]||null):(builder.dayAddresses[key(x.date,x.slot)]||builder.dayAddresses[x.date]||null)})),[builderSelections,builder.dayAddresses,builder.deliveryMode]);
 const confirmedAllergyRecipeIds=useMemo(()=>Object.keys(builder.allergyAcknowledged).filter(id=>builderSelections.some(x=>x.recipeId===id)),[builder.allergyAcknowledged,builderSelections]);
 const builderMissingAddresses=useMemo(()=>selectionPayload.filter(x=>!x.addressId),[selectionPayload]);
 const quoteBuilder=async()=>run(async()=>{
   if(!builder.outlet)throw new Error('Select an outlet from Find Meals first.');
   if(!builder.deliveryCity)throw new Error('Choose a package delivery city first.');
   if(!selectedCount)throw new Error('Select at least one meal.');
   if(!global.addresses.length)throw new Error('Add a delivery address first.');
   if(builderMissingAddresses.length)throw new Error('Select a delivery address for all scheduled meals.');
   if(guestPackageReady&&!global.profile)throw new Error('Complete your allergy and dietary preferences before continuing.');
   const wrongCity=selectionPayload.map(x=>global.addresses.find(a=>a.id===x.addressId)).find(a=>a&&a.city?.toLowerCase()!==(builder.deliveryCity||builder.outlet?.city||'').toLowerCase());
   if(wrongCity)throw new Error('The package is for '+(builder.deliveryCity||builder.outlet?.city)+', but '+wrongCity.label+' is in '+wrongCity.city+'. Add/select an address in the delivery city.');
   const pendingWarning=selectionPayload.map(x=>{
     const recipe=outletRecipes.find(r=>r.id===x.recipeId);
     const matched=allergyMatches(recipe,global.profile?.allergies||[]);
     return matched.length&&!builder.allergyAcknowledged?.[x.recipeId]?{x,recipe,matched}:null;
   }).find(Boolean);
   if(pendingWarning){
     setPicker({date:pendingWarning.x.mealDate,slot:Number(pendingWarning.x.mealSlot),current:{date:pendingWarning.x.mealDate,slot:Number(pendingWarning.x.mealSlot),recipeId:pendingWarning.x.recipeId,portion:pendingWarning.x.portionSize||1}});
     notify('Please review the allergy warning before continuing.','warning');
     return;
   }
   const q=await customer.quote({outletId:builder.outlet.id,deliveryMode:builder.deliveryMode,duration:builder.duration,selections:selectionPayload,discountCode:builder.discountCode||null,confirmedAllergyRecipeIds,deliveryCity:builder.deliveryCity});
   setBuilder(b=>({...b,quote:q,step:2}));
 });

 const subscribeBuilder=async()=>run(async()=>{if(!builder.quote)await quoteBuilder();const q=builder.quote||await customer.quote({outletId:builder.outlet.id,deliveryMode:builder.deliveryMode,duration:builder.duration,selections:selectionPayload,discountCode:builder.discountCode||null,confirmedAllergyRecipeIds,deliveryCity:builder.deliveryCity});const s=await customer.subscribe({outletId:builder.outlet.id,deliveryMode:builder.deliveryMode,duration:builder.duration,frequency:'Weekly',selections:selectionPayload,discountCode:builder.discountCode||null,confirmedAllergyRecipeIds,deliveryCity:builder.deliveryCity});setSubs(x=>[s,...x.filter(y=>y.id!==s.id)]);setGlobal(g=>({...g,subscriptions:[s,...g.subscriptions.filter(y=>y.id!==s.id)]}));setSelectedSubId(s.id);setBuilder(b=>({...b,quote:q,step:3}));setGuestPackageReady(false);try{localStorage.removeItem('healthapp.savedPackageDraft')}catch{}setActive('subscriptions');notify('Package created. Complete payment to confirm the order.')});

 const paySubscription=s=>{setPaymentSubId(s.id);setActive('payment')};
 const completeSandboxPayment=async s=>run(async()=>{const p=await customer.pay(s.id,'subscription-'+s.id+'-'+Date.now(),'MockSandbox');if(String(p?.status||'').toLowerCase()!=='paid')throw new Error('Sandbox payment was not completed.');await reload();setPaymentSubId('');setActive('subscriptions');notify('Payment successful. Your subscription is now paid.')});

 const normalizeMealRows=rows=>(rows||[]).map(r=>({...r,mealDate:normalizeMealDate(r.mealDate)}));
 const selectSub=async id=>{const sub=subs.find(x=>x.id===id);const anchor=weekStartForDate(sub?.nextDeliveryDate||todayISO());setSelectedSubId(id);setCalendarWeek(anchor);const rows=await run(()=>customer.mealSelections(id,anchor));setMealSelections(normalizeMealRows(rows));setActive('calendar')};
 const moveWeek=n=>{const d=dateObj(calendarWeek);d.setDate(d.getDate()+n*7);setCalendarWeek(d.toISOString().slice(0,10))};
 useEffect(()=>{if(selectedSubId&&active==='calendar')customer.mealSelections(selectedSubId,calendarWeek).then(rows=>setMealSelections(normalizeMealRows(rows))).catch(e=>setError(e.message))},[selectedSubId,calendarWeek,active]);
 const skipMeal=async row=>{if(!confirm(row.mealDate===todayISO()?'Skipping today may incur the ₹50 late-skip fee. Continue?':'Skip this meal?'))return;await run(async()=>{const x=await customer.skipMeal(row.subscriptionId,row.id,'Customer skipped meal');setMealSelections(ms=>ms.map(m=>m.id===row.id?x:m));notify(x.lateSkipFee>0?'Meal skipped; ₹50 late-skip fee applied':'Meal skipped')})};
 const skipDay=async date=>{const s=subs.find(x=>x.id===selectedSubId);if(!s)return;if(!confirm('Skip all scheduled meals for this day?'))return;await run(async()=>{const xs=await customer.skipDay(s.id,date,'Customer skipped day');setMealSelections(ms=>ms.map(m=>xs.find(x=>x.id===m.id)||m));notify('Day skipped')})};
 const rescheduleMeal=async()=>run(async()=>{const x=await customer.rescheduleMeal(reschedule.subscriptionId,reschedule.id,{newMealDate:reschedule.newDate,newMealSlot:Number(reschedule.newSlot),addressId:reschedule.addressId||null});setMealSelections(ms=>[...ms.map(m=>m.id===reschedule.id?{...m,status:'Rescheduled',rescheduledAtUtc:new Date().toISOString()}:m),x]);setReschedule(null);notify('Meal rescheduled')});

 const dashboardMeals=customerDashboard?.benefits?.mealsThisWeek??global.subscriptions.reduce((n,s)=>n+s.mealsPerWeek,0);
 const upcoming=useMemo(()=>customerDashboard?.todayMeals?.length?customerDashboard.todayMeals:mealSelections.filter(x=>x.status==='Scheduled').slice(0,6),[customerDashboard,mealSelections]);
 const tabs=[['dashboard','⌂','Dashboard'],['discover','🍽','Find Meals'],['builder','✦','Build Package'],['subscriptions','▣','My Subscriptions'],['calendar','◷','Meal Calendar'],['payment','₹','Payment'],['orders','🧾','Orders'],['addresses','⌂','Addresses'],['profile','♥','Health Profile'],['wallet','₹','Wallet']];
 const sideTabs=tabs.filter(x=>x[0]!=='builder');
 const pageTitle=tabs.find(x=>x[0]===active)?.[2]||'Dashboard';

 if(!user)return <PublicHome authMode={authMode} setAuthMode={setAuthMode} authForm={authForm} setAuthForm={setAuthForm} doAuth={doAuth} error={error} setError={setError}/>;

 return <div className="customerShell">{loading&&<div className="loadbar"/>}<aside className="sidebar"><div className="sideBrand"><div className="brandMark">H</div><div><b>HealthApp</b><small>Customer portal</small></div></div><div className="customerMini"><div className="avatar">{(user.firstName||'C')[0]}</div><div><b>{user.firstName} {user.lastName}</b><span>Customer</span></div></div><div className="sideSection">Your journey</div>{sideTabs.slice(0,4).map(t=><button key={t[0]} className={active===t[0]?'navItem active':'navItem'} onClick={()=>go(t[0])}><span>{t[1]}</span>{t[2]}</button>)}<div className="sideSection">Manage</div>{sideTabs.slice(4).map(t=><button key={t[0]} className={active===t[0]?'navItem active':'navItem'} onClick={()=>go(t[0])}><span>{t[1]}</span>{t[2]}</button>)}<div className="sideBottom"><div className="miniCredit">Wallet <b>{money(global.credit.balance)}</b></div><button className="logoutBtn" onClick={logout}>Log out</button></div></aside><section className="mainPanel"><header className="topbar"><div className="mobileTopLeft"><button className="mobileMenuBtn" onClick={()=>setMobileMenuOpen(true)} aria-label="Open menu">☰</button><div><h1>{pageTitle}</h1><span>{global.profile?.goal?GOALS.find(x=>x[0]===global.profile.goal)?.[1]:'Build your personalised meal plan'}</span></div></div><div className="topbarDesktopTitle"><h1>{pageTitle}</h1><span>{global.profile?.goal?GOALS.find(x=>x[0]===global.profile.goal)?.[1]:'Build your personalised meal plan'}</span></div><div className="headerActions">{guestPackageReady&&<button className="savedPackagePill" onClick={()=>go('builder')} title="Resume saved package"><span>🛒</span><div><b>Package ready</b><small>Resume</small></div></button>}<button className="iconBtn" onClick={()=>reload()} title="Refresh">↻</button><button className="profilePill" onClick={()=>go('profile')}><div className="avatar sm">{(user.firstName||'C')[0]}</div><div><b>{user.firstName}</b><small>{global.profile?.diet||'Set profile'}</small></div></button></div></header><main className={active==='discover'?'content discoverContent':'content'}>{error&&<div className="statusBanner error"><span><b>⚠ Something needs attention</b>{error}</span><button onClick={()=>setError('')}>×</button></div>}{toast&&<div className={'statusToast '+toastType}><span>{toastType==='success'?'✓':toastType==='warning'?'⚠':toastType==='info'?'ℹ':'×'}</span><div><b>{toastType==='success'?'Success':toastType==='warning'?'Warning':toastType==='info'?'Info':'Error'}</b><small>{toast}</small></div><button onClick={()=>setToast('')}>×</button></div>}{active==='dashboard'&&<Dashboard user={user} global={global} dashboard={customerDashboard} dashboardMeals={dashboardMeals} upcoming={upcoming} setActive={setActive}/>} {active==='discover'&&<Discover likedMeals={global.likedMeals} onToggleLikedMeal={toggleLikedMeal} outlets={availableOutlets===null?global.outlets:availableOutlets} cities={global.cities} customerAllergies={global.profile?.allergies||[]} addresses={global.addresses} selectedAddressId={selectedAddressId} setSelectedAddressId={setSelectedAddressId} selectedOutlet={selectedOutlet} setSelectedOutlet={setSelectedOutlet} menu={outletMenu} recipes={outletRecipes} category={outletCategory} setCategory={setOutletCategory} openOutlet={openOutlet} recipeView={recipeView} setRecipeView={setRecipeView} startBuilder={startBuilder} cityFilter={cityFilter} setCityFilter={changeDiscoveryCity} openAddressForCity={openAddressForCity}/>} {active==='builder'&&<Builder guestPackageReady={guestPackageReady} profile={global.profile} likedMeals={global.likedMeals} builder={builder} setBuilder={setBuilder} days={builderDays} menuMap={menuMap} recipes={outletRecipes} customerAllergies={global.profile?.allergies||[]} addresses={global.addresses} selectedCount={selectedCount} selectionPayload={selectionPayload} missingAddresses={builderMissingAddresses} quote={builder.quote} picker={picker} setPicker={setPicker} setSelection={setSelection} toggleDay={toggleDay} copyWeek={copyWeek} setDayAddress={setDayAddress} setMealAddress={setMealAddress} setBuilderDuration={setBuilderDuration} quoteBuilder={quoteBuilder} subscribeBuilder={subscribeBuilder} setActive={setActive}/>} {active==='subscriptions'&&<Subscriptions subs={subs} selectSub={selectSub} paySubscription={paySubscription}/>} {active==='calendar'&&<Calendar subs={subs} selectedSubId={selectedSubId} setSelectedSubId={selectSub} rows={mealSelections} week={calendarWeek} moveWeek={moveWeek} skipMeal={skipMeal} skipDay={skipDay} openReschedule={setReschedule}/>} {active==='payment'&&<PaymentPage subscription={subs.find(x=>x.id===paymentSubId)} onBack={()=>setActive('subscriptions')} onPay={completeSandboxPayment}/>} {active==='orders'&&<Orders orders={global.orders}/>} {active==='addresses'&&<Addresses addresses={global.addresses} cities={global.cities} city={cityFilter} setCity={setCityFilter} areas={global.areas} openNew={openNewAddress} edit={editAddress} remove={deleteAddress}/>} {active==='profile'&&<Profile form={profileForm} setForm={setProfileForm} save={saveProfile} profile={global.profile} allergens={global.allergens}/>} {active==='wallet'&&<Wallet credit={global.credit} transactions={global.transactions}/>}</main></section><div className={mobileMenuOpen?'mobileDrawerBackdrop open':'mobileDrawerBackdrop'} onClick={()=>setMobileMenuOpen(false)}><aside className="mobileDrawer" onClick={e=>e.stopPropagation()}><div className="mobileDrawerHead"><div className="sideBrand"><div className="brandMark">H</div><div><b>HealthApp</b><small>Customer portal</small></div></div><button className="iconBtn" onClick={()=>setMobileMenuOpen(false)}>×</button></div><div className="mobileCustomer"><div className="avatar">{(user.firstName||'C')[0]}</div><div><b>{user.firstName} {user.lastName}</b><span>{user.email}</span></div></div><div className="sideSection">Your journey</div>{sideTabs.slice(0,4).map(t=><button key={t[0]} className={active===t[0]?'navItem active':'navItem'} onClick={()=>go(t[0])}><span>{t[1]}</span>{t[2]}</button>)}<div className="sideSection">Manage</div>{sideTabs.slice(4).map(t=><button key={t[0]} className={active===t[0]?'navItem active':'navItem'} onClick={()=>go(t[0])}><span>{t[1]}</span>{t[2]}</button>)}<div className="mobileDrawerBottom"><div className="miniCredit">Wallet <b>{money(global.credit.balance)}</b></div><button className="logoutBtn" onClick={logout}>Log out</button></div></aside></div><nav className="mobileBottomNav">{[['dashboard','⌂','Home'],['subscriptions','▣','Plans'],['calendar','◷','Calendar'],['orders','🧾','Orders']].map(t=><button key={t[0]} className={active===t[0]?'mobileBottomItem active':'mobileBottomItem'} onClick={()=>go(t[0])}><span>{t[1]}</span><small>{t[2]}</small></button>)}<button className="mobileBottomItem" onClick={()=>setMobileMenuOpen(true)}><span>☰</span><small>More</small></button></nav>{addressModal&&<AddressModal form={addressForm} setForm={setAddressForm} mode={addressModal} areas={global.areas} cities={global.cities} city={cityFilter} setCity={setCityFilter} mapBusy={mapBusy} onMapPick={pickAddressLocation} onSave={saveAddress} onClose={()=>setAddressModal(null)}/>} {reschedule&&<RescheduleModal row={reschedule} addresses={global.addresses} onClose={()=>setReschedule(null)} onChange={setReschedule} onSave={rescheduleMeal}/>}</div>;
}

function Dashboard({user,global,dashboard,dashboardMeals,upcoming,setActive}){const activeSubs=dashboard?.activeSubscriptions?.length?dashboard.activeSubscriptions:global.subscriptions.filter(x=>x.status==='Active');const todayDeliveries=dashboard?.todayDeliveries||[];const todayMeals=dashboard?.todayMeals||upcoming||[];const benefits=dashboard?.benefits||{mealsThisWeek:dashboardMeals,proteinGramsThisWeek:0,caloriesThisWeek:0,subscriptionSavings:0,deliveryDaysThisWeek:0,activeSubscriptions:activeSubs.length};return <div className="page dashboardPage"><section className="dashboardWelcome"><div><span className="eyebrow">YOUR HEALTH JOURNEY</span><h2>Good {new Date().getHours()<12?'morning':new Date().getHours()<18?'afternoon':'evening'}, {user.firstName}!</h2><p>Here’s what is happening with your meals, deliveries and plan today.</p></div><button className="secondary" onClick={()=>setActive('calendar')}>View calendar →</button></section><div className="liveStats"><button className="liveStat delivery" onClick={()=>todayDeliveries.length&&setActive('orders')}><div className="liveIcon">🚚</div><div><span>Today’s delivery</span><b>{todayDeliveries[0]?todayDeliveries[0].status==='OutForDelivery'?'Out for delivery':todayDeliveries[0].status: 'No delivery today'}</b><small>{todayDeliveries[0]?todayDeliveries[0].deliveryWindow+(todayDeliveries[0].mealCount>1?' · '+todayDeliveries[0].mealCount+' meals':''):'Your next delivery will appear here'}</small></div><strong>›</strong></button><button className="liveStat" onClick={()=>setActive('subscriptions')}><div className="liveIcon">▣</div><div><span>Active subscriptions</span><b>{activeSubs.length}</b><small>{activeSubs.length?'Manage your plans':'Build your first package'}</small></div><strong>›</strong></button><button className="liveStat" onClick={()=>setActive('calendar')}><div className="liveIcon">🍽</div><div><span>Meals this week</span><b>{benefits.mealsThisWeek}</b><small>Across your active plans</small></div><strong>›</strong></button><button className="liveStat"><div className="liveIcon">♥</div><div><span>Nutrition planned</span><b>{benefits.proteinGramsThisWeek}g protein</b><small>{benefits.caloriesThisWeek.toLocaleString()} kcal this week</small></div></button></div><div className="dashboardGrid liveGrid"><section className="panel deliveryPanel"><div className="panelHead"><div><h3>Track today’s delivery</h3><p>Live delivery status from your outlet.</p></div>{todayDeliveries.length>0&&<button className="linkBtn" onClick={()=>setActive('orders')}>View orders →</button>}</div>{todayDeliveries.length?<div className="deliveryTrackList">{todayDeliveries.map(d=><article className="deliveryTrackCard" key={d.deliveryId}><div className="deliveryTrackHead"><div><b>{d.mealSlot} delivery</b><span>{d.deliveryWindow} · {d.mealCount} meal{d.mealCount===1?'':'s'}</span></div><span className={'deliveryPill '+String(d.status).toLowerCase()}>{d.status==='OutForDelivery'?'Out for delivery':d.status}</span></div><DeliveryTimeline status={d.status}/><div className="deliveryAddress"><span>📍</span><div><b>Delivery address</b><p>{d.address}</p></div></div></article>)}</div>:<div className="dashboardEmpty"><div className="emptyIcon">🚚</div><h3>No delivery scheduled today</h3><p>Your next delivery will appear here as soon as it is scheduled.</p><button className="secondary" onClick={()=>setActive('calendar')}>View upcoming meals</button></div>}</section><section className="panel mealsPanel"><div className="panelHead"><div><h3>Today’s meals</h3><p>Your meals arriving today.</p></div><button className="linkBtn" onClick={()=>setActive('calendar')}>View calendar →</button></div>{todayMeals.length?<div className="todayMealList">{todayMeals.map(m=><button className="todayMealRow" key={m.selectionId||m.id} onClick={()=>setActive('calendar')}><div className="todayMealImage">{m.imageUrl?<img src={getImg(m.imageUrl)} alt=""/>:<span>🍽</span>}</div><div><b>{m.recipeName}</b><span>{m.mealSlot} · {m.category} · {m.calories} kcal · {m.proteinGrams}g protein</span></div><strong>›</strong></button>)}</div>:<div className="dashboardEmpty compact"><p>No meals scheduled for today.</p><button className="linkBtn" onClick={()=>setActive('calendar')}>Open meal calendar →</button></div>}</section></div><div className="dashboardLower"><section className="panel"><div className="panelHead"><div><h3>Your active subscriptions</h3><p>Plans currently powering your meal schedule.</p></div><button className="linkBtn" onClick={()=>setActive('subscriptions')}>View all →</button></div>{activeSubs.length?<div className="dashboardSubList">{activeSubs.slice(0,3).map(s=><button className="dashboardSubCard" key={s.id} onClick={()=>setActive('subscriptions')}><div><span className="subStatus">{String(s.paymentStatus||'Pending').toLowerCase()==='paid'?'PAID':'ACTIVE'}</span><b>{s.planName}</b><small>{s.mealsPerWeek} meals · {s.deliveryMode==='OneDeliveryPerDay'?'One delivery/day':'Meal-by-meal'}</small></div><div><strong>{money(s.totalCharged)}</strong><small>Next {shortDate(String(s.nextDeliveryDate).slice(0,10)||todayISO())}</small></div></button>)}</div>:<div className="dashboardEmpty compact"><p>Build a personalised meal package to get started.</p><button className="primary" onClick={()=>setActive('discover')}>Find meals</button></div>}</section><section className="panel benefitsPanel"><div className="panelHead"><div><h3>Your benefits so far</h3><p>Based on your current active subscriptions.</p></div><span className="benefitPeriod">This week</span></div><div className="benefitGrid"><div><span>🥗</span><b>{benefits.mealsThisWeek}</b><small>Healthy meals planned</small></div><div><span>💪</span><b>+{benefits.proteinGramsThisWeek}g</b><small>Protein planned</small></div><div><span>♥</span><b>{benefits.caloriesThisWeek.toLocaleString()}</b><small>Calories planned</small></div><div><span>₹</span><b>{money(benefits.subscriptionSavings)}</b><small>Subscription savings</small></div></div><div className="benefitNotes"><p>✓ Flexible meal planning around your week</p><p>✓ Delivery days and multiple addresses supported</p><p>✓ Nutrition information shown before you choose</p></div></section></div><section className="tipsStrip"><span>💡</span><div><b>Get more from your plan</b><p>Keep your health profile up to date so meal choices stay aligned with your goals.</p></div><button className="secondary" onClick={()=>setActive('profile')}>Update profile</button></section></div>}

function DeliveryTimeline({status}){const steps=[['Preparing','Food is being prepared'],['PickedUp','Picked up by driver'],['OutForDelivery','Out for delivery'],['Delivered','Delivered']];const normalized=String(status||'Scheduled');const index=normalized==='Scheduled'?0:Math.max(0,steps.findIndex(x=>x[0]===normalized));return <div className="deliveryTimeline">{steps.map((s,i)=><div className={i<index?'timelineStep done':i===index?'timelineStep current':'timelineStep'} key={s[0]}><div className="timelineDot">{i<=index?'✓':'•'}</div><div><b>{s[0]==='OutForDelivery'?'Out for delivery':s[0]}</b><small>{i===index? (normalized==='Delivered'?'Delivered successfully':'Current status'):s[1]}</small></div></div>)}</div>}

function Stat({title,value,note,onClick}){return <button className="statCard" onClick={onClick}><span>{title}</span><b>{value}</b><small>{note} ↗</small></button>}

function Discover({outlets,cities,customerAllergies,addresses,selectedAddressId,setSelectedAddressId,selectedOutlet,setSelectedOutlet,menu,recipes,category,setCategory,openOutlet,recipeView,setRecipeView,startBuilder,cityFilter,setCityFilter,openAddressForCity,likedMeals,onToggleLikedMeal}){
 const[menuSlot,setMenuSlot]=useState(1);
 const[outletTab,setOutletTab]=useState('Menu');
 const[query,setQuery]=useState('');
 const[viewMode,setViewMode]=useState('grid');
 const[localFilter,setLocalFilter]=useState('All');
 const[addressPickerOpen,setAddressPickerOpen]=useState(false);
 const cityAddresses=addresses.filter(a=>a.city?.toLowerCase()===cityFilter.toLowerCase());
 const selectedAddress=addresses.find(a=>a.id===selectedAddressId);
 const filterFor=(x)=>{
   if(localFilter==='All')return true;
   if(['Veg','Vegan','NonVeg','Eggetarian','Pescatarian'].includes(localFilter))return x.category===localFilter;
   if(localFilter==='High Protein')return Number(x.proteinGrams)>=25;
   return true;
 };
 const normalizedQuery=query.trim().toLowerCase();
 const outletMatchesFilter=o=>{
   if(localFilter==='All')return true;
   const highlights=(o.healthHighlights||[]).join(' ').toLowerCase();
   if(localFilter==='High Protein')return highlights.includes('high protein')||highlights.includes('protein');
   if(localFilter==='Low Carb')return highlights.includes('low carb')||highlights.includes('keto');
   if(localFilter==='Vegan')return highlights.includes('vegan');
   if(localFilter==='Vegetarian')return highlights.includes('vegetarian')||highlights.includes('veg');
   if(localFilter==='Gluten Free')return highlights.includes('gluten free');
   return true;
 };
 const visibleOutlets=outlets.filter(o=>{
   const hay=[o.name,o.city,o.state,o.healthHighlights?.join(' ')].filter(Boolean).join(' ').toLowerCase();
   return (!normalizedQuery||hay.includes(normalizedQuery))&&outletMatchesFilter(o);
 });
 const menuForSlot=slot=>(menu||[]).filter(x=>Number(x.mealSlotValue)===slot).filter(filterFor);
 const chooseAddress=id=>{
   if(id==='__add__'){setAddressPickerOpen(false);openAddressForCity(cityFilter,false);return}
   const a=addresses.find(x=>x.id===id);
   if(!a)return;
   setSelectedAddressId(a.id);
   setCityFilter(a.city||cityFilter);
   setSelectedOutlet(null);
   setAddressPickerOpen(false);
 };
 const selectCity=value=>{
   setCityFilter(value);
   setSelectedOutlet(null);
   setQuery('');
   setLocalFilter('All');
   setSelectedAddressId('');
 };
 const selectedImage=selectedOutlet?.heroImageUrl||selectedOutlet?.logoUrl;
 const healthy=selectedOutlet?.healthHighlights||[];
 return <div className="page">
   <div className="discoverySearchRow wireListingToolbar">
     <div className="addressPickerWrap">
       <button className="locationAddressBar" onClick={()=>setAddressPickerOpen(v=>!v)} aria-expanded={addressPickerOpen}>
         <span className="locationAddressIcon">⌖</span>
         <span className="locationAddressText">
           <b>{selectedAddress ? (selectedAddress.city || cityFilter) : (cityFilter || 'Choose location')}</b>
           <small>{selectedAddress ? [selectedAddress.state, selectedAddress.pincode].filter(Boolean).join(', ') : 'Select an exact delivery pin'}</small>
         </span>
         <strong>⌄</strong>
       </button>
       {addressPickerOpen&&<div className="addressPickerMenu">
         <div className="addressPickerTitle"><span>DELIVERY ADDRESS</span><b>Choose where to deliver</b></div>
         {addresses.map(a=><button type="button" key={a.id} className={a.id===selectedAddressId?'addressPickerItem selected':'addressPickerItem'} onClick={()=>chooseAddress(a.id)}>
           <span className="addressPickerIcon">{a.label==='Home'?'⌂':a.label==='Office'?'▣':'⌖'}</span>
           <span className="addressPickerItemText"><b>{a.label}</b><small>{[a.areaName||a.locality,a.city,a.pincode].filter(Boolean).join(' · ')}</small></span>
           {a.id===selectedAddressId&&<strong>✓</strong>}
         </button>)}
         <button type="button" className="addAddressPicker" onClick={()=>chooseAddress('__add__')}>＋ Add new address</button>
       </div>}
     </div>

     <div className="discoverySearch">
       <span>⌕</span>
       <input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Search meals, cuisines or outlets..."/>
     </div>

     <label className="cityCompact">
       <span>City</span>
       <select value={cityFilter} onChange={e=>selectCity(e.target.value)}>
         {cities.map(x=><option key={x.city+'|'+x.state} value={x.city}>{x.city}</option>)}
       </select>
     </label>

     <button className={viewMode==='grid'?'viewToggle active':'viewToggle'} onClick={()=>setViewMode('grid')} aria-label="Grid view">▦ Grid</button>
     <button className={viewMode==='list'?'viewToggle active':'viewToggle'} onClick={()=>setViewMode('list')} aria-label="List view">☷ List</button>
   </div>

   <div className="discoveryFilterBar wireListingFilters">
     <div className="discoverFilters">
       {['All','High Protein','Low Carb','Vegan','Vegetarian','Gluten Free'].map(x=>
         <button key={x} className={localFilter===x?'discoverFilter active':'discoverFilter'} onClick={()=>{setLocalFilter(x);setCategory(x)}}>{x}</button>
       )}
     </div>
     <span className="count">{visibleOutlets.length} outlet{visibleOutlets.length===1?'':'s'} available</span>
   </div>

   {!selectedOutlet&&visibleOutlets.length>0&&<div className={viewMode==='grid'?'outletGrid discoveryGrid wireOutletGrid':'outletListView'}>
     {visibleOutlets.map(o=><article className="outletDiscoveryCard wireOutletCard" key={o.id} onClick={()=>{setOutletTab('Menu');setMenuSlot(1);openOutlet(o)}}>
       <div className="outletHeroThumb"><img src={o.heroImageUrl?getImg(o.heroImageUrl):IMAGE_FALLBACKS.hero} alt="" onError={e=>{e.currentTarget.src=IMAGE_FALLBACKS.hero}}/><button className="heartBtn" onClick={e=>e.stopPropagation()}>♡</button></div>
       <div className="outletDiscoveryBody">
         <div className="outletTitleLine"><div><h3>{o.name}</h3><p>{o.city}, {o.state} · {o.distanceKm?o.distanceKm+' km':'Nearby'}</p></div></div>
         <div className="outletRatingLine"><span>★</span><b>{Number(o.rating||4.8).toFixed(1)}</b><span>({o.reviewCount||0})</span></div>
         <div className="healthChipRow">{(o.healthHighlights||[]).slice(0,4).map((h,i)=><span key={i}>✓ {h}</span>)}</div>
         <div className="outletDiscoveryFoot"><button className="secondary smallBtn" onClick={e=>{e.stopPropagation();setOutletTab('Menu');setMenuSlot(1);openOutlet(o)}}>View menu →</button><button className="primary smallBtn" onClick={e=>{e.stopPropagation();cityAddresses.length?startBuilder(o):openAddressForCity(o.city,true)}}>{cityAddresses.length?'Build package':'Add address'}</button></div>
       </div>
     </article>)}
   </div>}

   {!selectedOutlet&&visibleOutlets.length===0&&<Empty title="No outlets found" text={normalizedQuery?'Try another search or remove the filters.':'Try another supported city or use a different delivery address.'}/>}


   {selectedOutlet&&<section className="panel outletMenuDetail exactWireframeOutlet">
     <div className="wireOutletBanner">
       <img src={selectedImage?getImg(selectedImage):IMAGE_FALLBACKS.hero} alt="" onError={e=>{e.currentTarget.src=IMAGE_FALLBACKS.hero}}/>
       <button className="wireBannerBack" onClick={()=>setSelectedOutlet(null)}>← Back to outlets</button>
       <button className="wireBannerHeart" aria-label="Save outlet">♡</button>
       <button className="wireBannerBuild" onClick={()=>cityAddresses.length?startBuilder(selectedOutlet):openAddressForCity(selectedOutlet.city,true)}>Build Package →</button>
       <div className="wireBannerBadges">{healthy.slice(0,4).map((h,i)=><span key={i}>✓ {h}</span>)}</div>
     </div>
     <div className="wireOutletIdentity">
       <div className="wireOutletLogo"><img src={selectedOutlet.logoUrl?getImg(selectedOutlet.logoUrl):IMAGE_FALLBACKS.logo} alt="" onError={e=>{e.currentTarget.src=IMAGE_FALLBACKS.logo}}/></div>
       <div className="wireOutletIdentityMain"><h3>{selectedOutlet.name}</h3><p>{selectedOutlet.city}, {selectedOutlet.state}{selectedOutlet.distanceKm ? ' · '+selectedOutlet.distanceKm+' km' : ''}</p><div className="wireRating"><span>★</span><b>{Number(selectedOutlet.rating||4.8).toFixed(1)}</b><span>({selectedOutlet.reviewCount||0} reviews)</span></div></div>

     </div>
     <div className="wireOutletTabs">{['Menu','About','Reviews','Location'].map(t=><button key={t} className={outletTab===t?'active':''} onClick={()=>setOutletTab(t)}>{t}</button>)}</div>
     {outletTab==='Menu'&&<>
       <div className="wireSlotTabs">{[[1,'☀','Morning','7 AM - 10 AM'],[2,'☀','Afternoon','12 PM - 2 PM'],[3,'☾','Evening','6 PM - 8 PM'],[4,'☾','Night','8 PM - 10 PM']].map(([id,icon,label,time])=><button key={id} className={menuSlot===id?'active':''} onClick={()=>setMenuSlot(id)}><span>{icon}</span><b>{label}</b><small>({time})</small></button>)}</div>
       <div className="wireMenuHeading"><div><h3>{['','Morning','Afternoon','Evening','Night'][menuSlot]} Meals</h3><p>Start your day with nutritious and balanced meals.</p></div><div className="chipRow">{['All','High Protein','Low Carb','Vegan','Vegetarian','Gluten Free'].map(cat=><button key={cat} className={localFilter===cat?'chip active':'chip'} onClick={()=>{setLocalFilter(cat);setCategory(cat)}}>{cat}</button>)}</div></div>
       <div className="wireMealGrid">{menuForSlot(menuSlot).map(x=>{const detail=recipes.find(r=>r.id===x.recipeId);return <article className="wireMealCard" key={x.id}><div className="wireMealImage"><img src={x.imageUrl?getImg(x.imageUrl):fallbackImg(x.category)} alt="" onError={e=>{e.currentTarget.src=fallbackImg(x.category)}}/><button type="button" className={likedMeals.some(m=>m.recipeId===x.recipeId)?'mealSave saved':'mealSave'} onPointerDown={e=>e.stopPropagation()} onMouseDown={e=>e.stopPropagation()} onClick={e=>{e.preventDefault();e.stopPropagation();onToggleLikedMeal(x.recipeId)}} aria-label="Save meal">{likedMeals.some(m=>m.recipeId===x.recipeId)?'♥':'♡'}</button></div><div className="wireMealBody" onClick={()=>detail&&setRecipeView(detail)}><h4>{x.recipeName}</h4><div className="wireMealMacroStrip"><div><span>Kcal</span><b>{x.calories}</b></div><div><span>Pro</span><b>{x.proteinGrams}g</b></div><div><span>Carb</span><b>{x.carbsGrams??0}g</b></div><div><span>Fib</span><b>{x.fiberGrams??0}g</b></div></div><div className="healthChipRow compact">{Number(x.proteinGrams)>=25&&<span>✓ High Protein</span>}{x.category==='Veg'&&<span>✓ Vegetarian</span>}</div><div className="wireMealPrice"><strong>{money(x.pricePerMeal)}</strong></div></div></article>})}</div>
       {!menuForSlot(menuSlot).length&&<Empty title={'No '+['','morning','afternoon','evening','night'][menuSlot]+' meals published'} text="The outlet has not added meals for this time slot yet."/>}
     </>}
     {outletTab==='About'&&<div className="wireInfoPanel"><h3>About {selectedOutlet.name}</h3><p>{selectedOutlet.about||'Fresh, healthy meals prepared with quality ingredients and balanced portions for your everyday routine.'}</p><div className="wireAboutGrid">{healthy.map((h,i)=><div key={i}><span>✓</span><b>{h}</b></div>)}</div></div>}
     {outletTab==='Reviews'&&<div className="wireReviewsPanel"><div className="wireReviewsSummary"><div><strong>{Number(selectedOutlet.rating||4.8).toFixed(1)}</strong><span>★★★★★</span><small>{selectedOutlet.reviewCount||0} reviews</small></div><p>Customers love the food quality, freshness and consistent portions.</p></div>{[['Priya','Great taste and very fresh.','2 days ago'],['Rahul','Good portions and reliable delivery.','1 week ago'],['Anita','Loved the weekly meal options.','2 weeks ago']].map((r,i)=><article className="wireReviewRow" key={i}><div className="reviewAvatar">{r[0][0]}</div><div><b>{r[0]}</b><span>★★★★★ · {r[2]}</span><p>{r[1]}</p></div></article>)}</div>}
     {outletTab==='Location'&&<div className="wireLocationPanel"><div><span className="eyebrow">DELIVERY LOCATION</span><h3>{selectedOutlet.city}, {selectedOutlet.state}</h3><p>This outlet delivers within its configured service radius from the outlet location.</p><div className="wireLocationStats"><span>Outlet postcode<b>{selectedOutlet.pincode}</b></span><span>Distance from you<b>{selectedOutlet.distanceKm ? selectedOutlet.distanceKm+' km' : 'Set address'}</b></span></div></div><div className="wireLocationMap"><MapContainer center={cityMapCenter(selectedOutlet.city)} zoom={11} scrollWheelZoom={false} className="outletLocationMap"><TileLayer url={MAP_TILE_URL} attribution={MAP_ATTRIBUTION}/><CircleMarker center={cityMapCenter(selectedOutlet.city)} radius={10}/></MapContainer></div></div>}
     <div className="outletMenuCta"><div><b>Ready to personalise your week?</b><span>Choose your days and meals in the weekly planner.</span></div><button className="primary big" onClick={()=>cityAddresses.length?startBuilder(selectedOutlet):openAddressForCity(selectedOutlet.city,true)}>Build Package →</button></div>
   </section>}

   {recipeView&&<RecipeModal recipe={recipeView} onClose={()=>setRecipeView(null)}/>}
 </div>
}
function allergyMatches(recipe,customerAllergies){if(!recipe||!customerAllergies?.length)return[];const ids=new Set(customerAllergies.map(a=>a.id));const hits=[];for(const a of recipe.allergens||[])if(ids.has(a.id))hits.push({allergen:a.name,ingredient:null});for(const i of recipe.ingredients||[])for(const a of i.allergens||[])if(ids.has(a.id))hits.push({allergen:a.name,ingredient:i});const seen=new Set();return hits.filter(x=>{const k=x.allergen+'|'+(x.ingredient?.ingredientId||'recipe');if(seen.has(k))return false;seen.add(k);return true});}
function RecipeCard({recipe,customerAllergies,onClick}){const matched=allergyMatches(recipe,customerAllergies);return <button className="recipeCard" onClick={onClick}><div className="recipeImage">{recipe.imageUrl?<img src={getImg(recipe.imageUrl)} alt=""/>:<span>🍱</span>}<span className="categoryTag">{recipe.category}</span>{matched.length>0&&<span className="allergyBadge">⚠ Allergy match</span>}</div><div className="recipeBody"><div className="recipeTitle"><div><h4>{recipe.name}</h4><span className="muted">{recipe.calories} kcal · {recipe.proteinGrams}g protein</span></div><strong>{money(recipe.pricePerMeal)}</strong></div><p>{recipe.description||'Wholesome meal prepared by the outlet.'}</p><div className="macroRow"><span>Carbs {recipe.carbsGrams}g</span><span>Fat {recipe.fatGrams}g</span><span>{recipe.largePricePerMeal>recipe.pricePerMeal?'Large portion available':''}</span></div></div></button>}
function RecipeModal({recipe,onClose}){return <Modal title={recipe.name} onClose={onClose}><div className="recipeDetail"><div className="recipeDetailImage">{recipe.imageUrl?<img src={getImg(recipe.imageUrl)} alt=""/>:<span>🍱</span>}</div><div><div className="tag">{recipe.category}</div><p>{recipe.description}</p><div className="nutritionGrid"><StatBox label="Calories" value={recipe.calories+' kcal'}/><StatBox label="Protein" value={recipe.proteinGrams+' g'}/><StatBox label="Carbs" value={recipe.carbsGrams+' g'}/><StatBox label="Fiber" value={(recipe.fiberGrams??0)+' g'}/></div><h4>Ingredients</h4><div className="ingredientList">{(recipe.ingredients||[]).length?(recipe.ingredients||[]).map(i=><div key={i.ingredientId}><div><b>{i.name}</b><span>{i.quantity} {i.unit}</span></div>{(i.allergens||[]).length>0&&<small className="ingredientAllergen">⚠ {i.allergens.map(a=>a.name).join(', ')}</small>}</div>):<p>Ingredient details not provided by outlet.</p>}</div><h4>Allergens</h4><div className="allergenList">{(recipe.allergens||[]).length?(recipe.allergens||[]).map(a=><span key={a.id}>{a.name}</span>):<p>No allergens listed</p>}</div><h4>Tags</h4><p>{recipe.tags||'—'}</p><div className="priceLine"><span>Regular</span><b>{money(recipe.pricePerMeal)}</b><span>Large</span><b>{money(recipe.largePricePerMeal)}</b></div></div></div></Modal>}
function StatBox({label,value}){return <div className="statBox"><span>{label}</span><b>{value}</b></div>}

function Builder({guestPackageReady,profile,likedMeals,builder,setBuilder,days,menuMap,recipes,customerAllergies,addresses,selectedCount,selectionPayload,missingAddresses,quote,picker,setPicker,setSelection,toggleDay,copyWeek,setDayAddress,setMealAddress,setBuilderDuration,quoteBuilder,subscribeBuilder,setActive}){
 const weeks=Array.from({length:builder.weeks},(_,i)=>i+1);
 const deliveryAddresses=addresses.filter(a=>a.city?.toLowerCase()===(builder.deliveryCity||builder.outlet?.city||'').toLowerCase());
 const picked=(date,slot)=>builder.selections[key(date,slot)];
 const getRecipe=(date,slot)=>{const v=picked(date,slot);if(!v)return null;const options=menuMap[key(dayId(date),slot)]||[];return options.find(x=>x.recipeId===v.recipeId)||null};
 const visibleSlots=SLOT.filter(s=>menuMapForOutletSlot(s.id,menuMap));
 const mealValue=selectionPayload.reduce((sum,x)=>{const opts=menuMap[key(dayId(x.mealDate),x.mealSlot)]||[];const m=opts.find(y=>y.recipeId===x.recipeId);return sum+(m?Number(x.portionSize===2?m.largePricePerMeal:m.pricePerMeal):0)},0);
 const selectedMealRows=selectionPayload.slice(0,8).map(x=>{const opts=menuMap[key(dayId(x.mealDate),x.mealSlot)]||[];const meal=opts.find(y=>y.recipeId===x.recipeId);return {...x,meal}});
 const selectedWarnings=useMemo(()=>selectionPayload.map(x=>{const recipe=recipes.find(r=>r.id===x.recipeId);const matched=allergyMatches(recipe,customerAllergies);return matched.length?{...x,recipe,matched}:null}).filter(Boolean),[selectionPayload,recipes,customerAllergies]);
 const unacknowledgedWarnings=selectedWarnings.filter(x=>!builder.allergyAcknowledged?.[x.recipeId]);
 const detailsComplete=Boolean(profile)&&addresses.length>0&&!missingAddresses.length;
 return <div className="page"><section className="builderTop wireBuilderTop"><div><span className="eyebrow">CUSTOM PACKAGE BUILDER</span><h2>Build your meals, your way.</h2><p>Pick active days, meal slots, portions and delivery addresses. The meals you actually select become the package.</p></div><div className="builderSteps"><span className="done">1 <b>Choose</b></span><span className={builder.step>=2?'done':''}>2 <b>Review</b></span><span className={builder.step>=3?'done':''}>3 <b>Created</b></span></div></section>{guestPackageReady&&<section className="guestPackageResumePanel"><div className="guestPackageResumeIcon">✓</div><div className="guestPackageResumeCopy"><span className="eyebrow">SAVED GUEST PACKAGE</span><h3>Your package is ready to continue</h3><p>Your selected meals are preserved. Complete your delivery address and allergy/dietary preferences before payment.</p><div className="guestPackageChecklist"><span className={addresses.length?'complete':''}>{addresses.length?'✓':'1'} Delivery address</span><span className={profile?'complete':''}>{profile?'✓':'2'} Allergy & dietary preferences</span><span className={unacknowledgedWarnings.length?'attention':selectedCount?'complete':''}>{unacknowledgedWarnings.length?'⚠':'3'} Meal safety review</span></div></div><div className="guestPackageResumeActions"><button className="secondary smallBtn" onClick={()=>setActive('addresses')}>{addresses.length?'Review address':'Add address'}</button><button className="secondary smallBtn" onClick={()=>setActive('profile')}>{profile?'Review preferences':'Set preferences'}</button></div></section>}<div className="builderControls"><label>Package delivery city<input value={builder.deliveryCity||builder.outlet?.city||''} readOnly/><small>Your home/residence city can be different. Every package is tied to this delivery city and requires an address in the same city.</small></label><label>Outlet<select value={builder.outlet?.id||''} readOnly><option>{builder.outlet?.name||'Select an outlet from Find Meals'}</option></select></label><label>Duration<select value={builder.duration} onChange={e=>setBuilderDuration(e.target.value)}>{DURATIONS.map(d=><option key={d.id} value={d.id}>{d.label}</option>)}</select></label><label>Start date<input type="date" value={builder.startDate} onChange={e=>setBuilder(b=>({...b,startDate:e.target.value,quote:null}))}/></label><label>Delivery mode<select value={builder.deliveryMode} onChange={e=>setBuilder(b=>({...b,deliveryMode:e.target.value,quote:null}))}><option value="OneDeliveryPerDay">One delivery per day</option><option value="IndividualMealDelivery">Individual meal delivery</option></select></label><label>Discount code<input value={builder.discountCode} onChange={e=>setBuilder(b=>({...b,discountCode:e.target.value,quote:null}))} placeholder="Optional"/></label></div><div className="builderInfo"><div><b>{selectedCount}</b><span>meals selected</span></div><div><b>{days.filter(d=>d.week===1&&isWeekDayActive(builder,1,d.date)).length}</b><span>active days in week 1</span></div><div><b>{money(mealValue)}</b><span>meal value before discounts</span></div><button className="secondary" onClick={()=>setBuilder(b=>({...b,selections:{},quote:null}))}>Clear meals</button></div><div className="builderLayout"><section className="panel builderCalendarPanel"><div className="builderHead"><div><h3>Choose your meals</h3><p>Each week is editable. Copy week 1 to later weeks when you want a starting template, then change any week independently.</p></div><div className="weekTools">{weeks.length>1&&<button className="secondary" onClick={()=>copyWeek(1)}>Copy week 1 → all weeks</button>}<button className="secondary" onClick={()=>setActive('addresses')}>Manage addresses</button></div></div>{weeks.map(w=><div className="weekBlock" key={w}><div className="weekTitle"><div><h4>Week {w}</h4><span>{formatDate(days[(w-1)*7]?.date||builder.startDate)} – {formatDate(days[(w-1)*7+6]?.date||addDays(builder.startDate,(w-1)*7+6))}</span></div><div className="weekDays">{DAYS.map(d=><button key={d.id} className={(builder.weekActiveDays[w]||[]).includes(d.id)?'dayChip active':'dayChip'} onClick={()=>toggleDay(w,d.id)}>{d.short}</button>)}</div></div><div className="mealMatrix"><div className="matrixHeader"><div>Day</div>{visibleSlots.map(s=><div key={s.id}>{s.icon} {s.label}</div>)}</div>{days.filter(d=>d.week===w).map(d=><div className={'matrixRow '+(!isWeekDayActive(builder,w,d.date)?'inactive':'')} key={d.date}><div className="dayCell"><b>{dayName(dayId(d.date))}</b><span>{shortDate(d.date)}</span>{isWeekDayActive(builder,w,d.date)&&<><label className="tinyLabel">{builder.deliveryMode==='OneDeliveryPerDay'?'Day delivery address':'Default day address'}<select value={builder.dayAddresses[d.date]||''} onChange={e=>setDayAddress(d.date,e.target.value)}><option value="">Select</option>{deliveryAddresses.map(a=><option key={a.id} value={a.id}>{a.label} · {a.areaName}</option>)}</select></label><small>{countDaySelections(builder,d.date)} meal(s) selected</small></>}{!isWeekDayActive(builder,w,d.date)&&<small>Day disabled</small>}</div>{visibleSlots.map(s=>{const r=getRecipe(d.date,s.id);const opts=menuMap[key(dayId(d.date),s.id)]||[];const v=picked(d.date,s.id);return <div className="mealCell" key={s.id}>{!isWeekDayActive(builder,w,d.date)?<div className="disabledCell">Not active</div>:r?<div className="selectedMealWrap"><button className="selectedMeal" onClick={()=>setPicker({date:d.date,slot:s.id,current:v})}><div className="miniImage">{r.imageUrl?<img src={getImg(r.imageUrl)} alt=""/>:<span>🍱</span>}</div><div><b>{r.recipeName}</b><span>{r.category} · {v.portion===2?'Large':'Regular'}</span></div><strong>{money(v.portion===2?r.largePricePerMeal:r.pricePerMeal)}</strong></button>{builder.deliveryMode==='IndividualMealDelivery'&&<select className="cellAddress" value={builder.dayAddresses[key(d.date,s.id)]||builder.dayAddresses[d.date]||''} onChange={e=>setMealAddress(d.date,s.id,e.target.value)}><option value="">Meal address</option>{deliveryAddresses.map(a=><option key={a.id} value={a.id}>{a.label}</option>)}</select>}</div>:<button className="emptyMeal" onClick={()=>setPicker({date:d.date,slot:s.id,current:null})}><span>+</span><b>Add meal</b><small>{opts.length?opts.length+' choices':'No outlet menu'}</small></button>}</div>})}</div>)}</div></div>)}</section><aside className="builderSummaryCard"><div className="builderSummaryHead"><span className="eyebrow">YOUR MEALS</span><h3>{selectedCount} selected</h3><p>{builder.outlet?.name||'Outlet'} · {builder.duration}</p></div><div className="builderSummaryList">{selectedMealRows.map((x,i)=><div className="builderSummaryMeal" key={x.date+'_'+x.slot+'_'+x.recipeId+'_'+i}><div className="summaryMealImage">{x.meal?.imageUrl?<img src={getImg(x.meal.imageUrl)} alt=""/>:<span>🍱</span>}</div><div><b>{x.meal?.recipeName||'Meal'}</b><span>{shortDate(x.mealDate)} · {slotName(x.mealSlot)}</span></div><strong>{money(x.portionSize===2?(x.meal?.largePricePerMeal||0):(x.meal?.pricePerMeal||0))}</strong></div>)}{selectedCount>8&&<div className="summaryMore">+ {selectedCount-8} more meal{selectedCount-8===1?'':'s'}</div>}{!selectedCount&&<div className="summaryEmpty">Your selected meals will appear here.</div>}</div><div className="builderSummaryTotals"><div><span>Meal total</span><b>{money(mealValue)}</b></div>{quote&&<div><span>Delivery + fees</span><b>{money(Number(quote.totalCharged)-Number(quote.netMealAmount))}</b></div>}<div className="summaryGrand"><span>{quote?'Total payable':'Before discounts'}</span><b>{quote?money(quote.totalCharged):money(mealValue)}</b></div></div><button className="primary big" onClick={quoteBuilder}>Review package →</button></aside></div><div className="builderFooter"><div><span>{selectedCount} meal selections</span><small>{missingAddresses.length?'Select addresses before quoting.':builder.deliveryMode==='OneDeliveryPerDay'?'One delivery fee per active day.':'Delivery fee is calculated for each meal.'}</small></div><button className="primary big" onClick={quoteBuilder}>Review package →</button></div>{quote&&<section className="checkoutPanel"><div className="checkoutSummary"><Line label="Gross meal amount" value={money(quote.grossMealAmount)}/><Line label="Package / code discount" value={'- '+money(quote.subscriptionDiscountAmount)}/><Line label="Net meal amount" value={money(quote.netMealAmount)}/><Line label="Restaurant GST" value={money(quote.restaurantGstAmount)}/><Line label="Delivery fee" value={money(quote.deliveryFee)}/><Line label="HealthApp service fee" value={money(quote.platformServiceFee)}/><Line label="Service fee GST" value={money(quote.platformServiceGst)}/><div className="totalLine"><span>Total payable</span><strong>{money(quote.totalCharged)}</strong></div></div><div className="quoteNotes"><h3>Review before creating</h3><div className="quoteCards">{quote.deliveryQuotes?.map(x=><div key={x.addressId}><span>{x.areaName}</span><b>{x.distanceKm} km</b><strong>{money(x.deliveryFee)}</strong></div>)}</div>{quote.allergyWarnings?.length>0&&<div className="quoteWarning"><b>⚠ Allergy caution</b>{quote.allergyWarnings.map(w=><div key={w.recipeId}><strong>{w.recipeName}</strong><p>{w.message}</p>{w.matchedIngredients?.length>0&&<small>Matched ingredients: {w.matchedIngredients.join(', ')}</small>}</div>)}</div>}<p>Late skipping on or after the delivery day can add a ₹50 late-skip fee. Unused meals can be rescheduled within the subscription rules instead of being automatically refunded.</p><button className="primary big" disabled={quote.requiresAllergyConfirmation} onClick={subscribeBuilder}>{quote.requiresAllergyConfirmation?'Confirm allergy warnings first':'Create package'}</button></div></section>}{picker&&<MealPicker picker={picker} menuMap={menuMap} recipes={recipes} customerAllergies={customerAllergies} likedMeals={likedMeals} current={picker.current} onClose={()=>setPicker(null)} onPick={(recipeId,portion,confirmed)=>{setSelection(picker.date,picker.slot,recipeId,portion,confirmed);setPicker(null)}}/>}</div>
}
function menuMapForOutletSlot(slot,map){return Object.keys(map).some(k=>Number(k.slice(k.lastIndexOf('_')+1))===Number(slot)&&map[k]?.length)}
function isWeekDayActive(builder,w,date){return Boolean(builder.weekActiveDays[w]?.includes(dayId(date)))}
function countDaySelections(builder,date){return Object.values(builder.selections).filter(x=>x&&x.date===date).length}
function Line({label,value}){return <div className="summaryLine"><span>{label}</span><b>{value}</b></div>}

function MealPicker({picker,menuMap,recipes,customerAllergies,likedMeals,current,onClose,onPick}){
 const options=menuMap[key(dayId(picker.date),picker.slot)]||[];
 const[category,setCategory]=useState('All');
 const[portion,setPortion]=useState(current?.portion||1);
 const[warning,setWarning]=useState(null);
 const likedIds=new Set((likedMeals||[]).map(x=>x.recipeId));
 const cats=['All',...Array.from(new Set(options.map(x=>x.category)))];
 const filtered=category==='All'?options:options.filter(x=>x.category===category);
 const likedFiltered=filtered.filter(x=>likedIds.has(x.recipeId));
 const otherFiltered=filtered.filter(x=>!likedIds.has(x.recipeId));
 const choose=x=>{
   const detail=recipes.find(r=>r.id===x.recipeId);
   const matched=allergyMatches(detail,customerAllergies);
   if(matched.length){setWarning({item:x,detail,matched});return}
   onPick(x.recipeId,portion,true);
 };
 const card=x=><article className="pickerCard" key={x.recipeId}>
   <div className="miniLargeImage">{x.imageUrl?<img src={getImg(x.imageUrl)} alt=""/>:<span>🍱</span>}</div>
   <div className="pickerBody">
     <div className="pickerHeading">
       <div><h4>{x.recipeName}</h4><span>{x.calories} kcal · {x.proteinGrams}g protein</span></div>
       <span className="tag">{x.category}</span>
     </div>
     <div className="priceChoices"><button className={portion===1?'choice active':'choice'} onClick={()=>setPortion(1)}>Regular {money(x.pricePerMeal)}</button><button className={portion===2?'choice active':'choice'} onClick={()=>setPortion(2)}>Large {money(x.largePricePerMeal)}</button></div>
     <button className="primary" onClick={()=>choose(x)}>Select meal</button>
   </div>
 </article>;
 return <Modal title={slotName(picker.slot)+' · '+formatDate(picker.date)} onClose={onClose}>
   <div className="chipRow">{cats.map(c=><button key={c} className={category===c?'chip active':'chip'} onClick={()=>setCategory(c)}>{c}</button>)}</div>
   {likedFiltered.length>0&&<section className="likedMealsPickerSection">
     <div className="likedMealsPickerHeading"><div><span className="eyebrow">YOUR FAVOURITES</span><h3>Liked Meals</h3></div><span>{likedFiltered.length} available</span></div>
     <div className="pickerGrid">{likedFiltered.map(card)}</div>
   </section>}
   <section className="allMealsPickerSection">
     <div className="likedMealsPickerHeading"><div><span className="eyebrow">OUTLET MENU</span><h3>{likedFiltered.length?'All '+slotName(picker.slot)+' Meals':slotName(picker.slot)+' Meals'}</h3></div><span>{otherFiltered.length+(likedFiltered.length?likedFiltered.length:0)} choices</span></div>
     <div className="pickerGrid">{otherFiltered.map(card)}</div>
   </section>
   {!filtered.length&&<Empty title="No recipes in this slot" text="Ask the outlet to add a menu item for this day and slot."/>}
   {filtered.length>0&&!likedFiltered.length&&likedMeals?.length>0&&<div className="likedEmptyHint">Your liked meals are not available for this day and time slot. Choose from the current outlet menu below.</div>}
   {warning&&<div className="warningOverlay"><div className="warningCard"><div className="warningIcon">⚠</div><h3>Allergy caution</h3><p><b>{warning.detail?.name||warning.item.recipeName}</b> contains or may contain <strong>{Array.from(new Set(warning.matched.map(x=>x.allergen))).join(', ')}</strong>, matching an allergy saved in your HealthApp profile.</p><p className="warningFine">Please review the full ingredient list and outlet information. HealthApp cannot guarantee absence of cross-contact.</p>{warning.matched.some(x=>x.ingredient)&&<div className="warningIngredients"><b>Matched ingredients</b>{warning.matched.filter(x=>x.ingredient).map((x,i)=><span key={i}>{x.ingredient.name} — {x.ingredient.quantity} {x.ingredient.unit} · {x.allergen}</span>)}</div>}<div className="modalActions"><button className="secondary" onClick={()=>setWarning(null)}>Choose another meal</button><button className="primary" onClick={()=>{onPick(warning.item.recipeId,portion,true);setWarning(null)}}>I understand, continue</button></div></div></div>}
 </Modal>;
}

function Subscriptions({subs,selectSub,paySubscription}){return <div className="page"><section className="pageIntro"><div><span className="eyebrow">YOUR PLAN</span><h2>My subscriptions</h2><p>Review package value, delivery mode, selected meals and payment state.</p></div></section><div className="subscriptionCards">{subs.map(s=>{const paid=String(s.paymentStatus||'Pending').toLowerCase()==='paid';return <article className="subscriptionCard" key={s.id}><div className={paid?'subStatus paid':'subStatus'}>{paid?'PAID':s.status}</div><h3>{s.planName}</h3><p>{s.frequency} · {s.mealsPerWeek} selected meals · {s.deliveryMode==='OneDeliveryPerDay'?'One delivery/day':'Meal-by-meal delivery'}</p><div className="subMetrics"><div><span>Package total</span><b>{money(s.totalCharged)}</b></div><div><span>Next delivery</span><b>{shortDate(s.nextDeliveryDate?.slice?.(0,10)||todayISO())}</b></div><div><span>Payment</span><b>{paid?'Paid':'Pending'}</b></div></div><div className="subActions"><button className="secondary" onClick={()=>selectSub(s.id)}>Open calendar</button>{paid?<button className="paidButton" disabled>Paid ✓</button>:<button className="primary" onClick={()=>paySubscription(s)}>Pay / confirm</button>}</div></article>})}{!subs.length&&<Empty title="No subscriptions yet" text="Build a custom package from an outlet menu to get started."/>}</div></div>}

function PaymentPage({subscription,onBack,onPay}){const[method,setMethod]=useState('card');const[cardName,setCardName]=useState('Demo1 Customer');const[cardNumber,setCardNumber]=useState('4111 1111 1111 1111');const[expiry,setExpiry]=useState('12/30');const[cvv,setCvv]=useState('123');const[upi,setUpi]=useState('demo@upi');const[processing,setProcessing]=useState(false);if(!subscription)return <div className="page"><Empty title="No payment selected" text="Choose Pay / confirm from a subscription first." action="Back to subscriptions" onClick={onBack}/></div>;const submit=async()=>{setProcessing(true);try{await new Promise(r=>setTimeout(r,900));await onPay(subscription)}finally{setProcessing(false)}};return <div className="page"><section className="paymentTop"><div><span className="eyebrow">SECURE CHECKOUT · SANDBOX</span><h2>Complete your payment</h2><p>This is a local test checkout. No real money is charged and no gateway account is required.</p></div><button className="secondary" onClick={onBack}>← Back</button></section><div className="paymentLayout"><section className="panel paymentCard"><div className="sandboxBadge">TEST MODE · MOCK GATEWAY</div><div className="paymentMethods"><button className={method==='card'?'method active':'method'} onClick={()=>setMethod('card')}>💳 Card</button><button className={method==='upi'?'method active':'method'} onClick={()=>setMethod('upi')}>⚡ UPI</button></div>{method==='card'?<div className="formGrid"><label className="span2">Cardholder name<input value={cardName} onChange={e=>setCardName(e.target.value)} placeholder="Test Customer"/></label><label className="span2">Card number<input value={cardNumber} onChange={e=>setCardNumber(e.target.value)} inputMode="numeric"/></label><label>Expiry<input value={expiry} onChange={e=>setExpiry(e.target.value)} placeholder="MM/YY"/></label><label>CVV<input value={cvv} onChange={e=>setCvv(e.target.value)} inputMode="numeric"/></label></div>:<label>UPI ID<input value={upi} onChange={e=>setUpi(e.target.value)} placeholder="name@upi"/></label>}<div className="sandboxHint"><b>Test credentials</b><span>Card: 4111 1111 1111 1111 · Expiry: any future date · CVV: any 3 digits</span><span>UPI: any test UPI ID, for example demo@upi</span></div><button className="primary big payNow" disabled={processing} onClick={submit}>{processing?'Processing sandbox payment…':'Pay '+money(subscription.totalCharged)}</button><small className="paymentLegal">Sandbox only. The backend records a MockSandbox payment transaction and confirms the package order.</small></section><aside className="panel paymentSummary"><div className="paymentLogo">H</div><span className="eyebrow">ORDER SUMMARY</span><h3>{subscription.planName}</h3><div className="paymentSummaryRows"><div><span>Meals</span><b>{subscription.mealsPerWeek}</b></div><div><span>Delivery mode</span><b>{subscription.deliveryMode==='OneDeliveryPerDay'?'One delivery / day':'Meal-by-meal'}</b></div><div><span>Next delivery</span><b>{shortDate(subscription.nextDeliveryDate?.slice?.(0,10)||todayISO())}</b></div></div><div className="paymentTotal"><span>Total payable</span><strong>{money(subscription.totalCharged)}</strong></div><div className="secureNote">🔒 Test payment · no real charge</div></aside></div></div>}

function Calendar({subs,selectedSubId,setSelectedSubId,rows,week,moveWeek,skipMeal,skipDay,openReschedule}){const sub=subs.find(x=>x.id===selectedSubId);const grouped=useMemo(()=>{const m={};for(const r of rows)(m[normalizeMealDate(r.mealDate)]??=[]).push(r);return m},[rows]);return <div className="page"><section className="calendarTop"><div><span className="eyebrow">MEAL SCHEDULE</span><h2>Meal calendar</h2><p>Manage meals, late skips and unused-meal rescheduling.</p></div><div className="calendarControls"><select value={selectedSubId} onChange={e=>setSelectedSubId(e.target.value)}><option value="">Choose subscription</option>{subs.map(s=><option key={s.id} value={s.id}>{s.planName}</option>)}</select><div className="weekNav"><button className="iconBtn" onClick={()=>moveWeek(-1)}>←</button><b>Week of {formatDate(week)}</b><button className="iconBtn" onClick={()=>moveWeek(1)}>→</button></div></div></section>{!sub?<Empty title="Select a subscription" text="Choose a package above to manage its meal calendar."/>:<div className="calendarGrid">{Array.from({length:7},(_,i)=>addDays(week,i)).map(date=><section className="dayPanel" key={date}><div className="dayPanelHead"><div><b>{dayName(dayId(date))}</b><span>{shortDate(date)}</span></div><button className="dangerText" onClick={()=>skipDay(date)}>Skip day</button></div>{(grouped[date]||[]).map(r=><div className="mealRow" key={r.id}><div className="mealIcon">{r.status==='Delivered'?'✓':r.status==='Unused'?'↻':'🍽'}</div><div className="mealInfo"><b>{r.recipeName}</b><span>{slotName(r.mealSlot)} · {r.category} · {r.status}</span><small>{money(r.mealPrice)} meal · {money(r.deliveryFee)} delivery {r.lateSkipFee>0?'· ₹50 late skip fee':''}</small></div><div className="mealActions">{r.status==='Scheduled'&&<button className="secondary smallBtn" onClick={()=>skipMeal(r)}>Skip</button>}{r.status==='Unused'&&<button className="primary smallBtn" onClick={()=>openReschedule({...r,newDate:addDays(date,1),newSlot:r.mealSlot,addressId:''})}>Reschedule</button>}</div></div>)}{!(grouped[date]||[]).length&&<div className="emptyDay">No scheduled meals</div>}</section>)}</div>}</div>}

function Orders({orders}){return <div className="page"><section className="pageIntro"><div><span className="eyebrow">ORDER HISTORY</span><h2>Orders & deliveries</h2><p>Package orders and their current state.</p></div></section><section className="panel"><div className="orderTable">{orders.map(o=><div className="orderRow" key={o.id}><div><b>Order {String(o.id).slice(0,8)}…</b><span>{formatDate(o.deliveryDate?.slice?.(0,10)||todayISO())}</span></div><div><span>Address</span><b>{o.address}</b></div><div><span>Status</span><b className="statusText">{o.status}</b></div><strong>{money(o.total)}</strong></div>)}{!orders.length&&<Empty title="No orders yet" text="Your package orders will appear here after subscription creation."/>}</div></section></div>}

function Addresses({addresses,cities,city,setCity,openNew,edit,remove}){return <div className="page"><section className="pageIntro"><div><span className="eyebrow">DELIVERY LOCATIONS</span><h2>My addresses</h2><p>Save any delivery location in a HealthApp-enabled city. Outlet serviceability is checked later from the exact pin.</p></div><button className="primary" onClick={openNew}>+ Add address</button></section><section className="panel addressCityPanel"><div className="toolbar"><label>City<select value={city} onChange={e=>setCity(e.target.value)}>{cities.map(x=><option key={x.city+'|'+x.state} value={x.city}>{x.city} · {x.state}</option>)}</select></label><span className="count">{addresses.length} saved address{addresses.length===1?'':'es'}</span></div><div className="cityAddressHint">You can save an exact location even when no outlet currently delivers there. HealthApp will show serviceable outlets when you use that address.</div></section><section className="panel"><div className="addressList">{addresses.map(a=><article className={'addressCard '+(a.isDefault?'default':'')} key={a.id}><div className="addressHead"><div><span className="addressLabel">{a.label}</span>{a.isDefault&&<span className="defaultTag">Default</span>}</div><div><button className="linkBtn" onClick={()=>edit(a)}>Edit</button><button className="dangerText" onClick={()=>remove(a)}>Delete</button></div></div><b>{a.addressLine1}</b><p>{a.addressLine2}</p><span>{[a.areaName,a.city].filter(Boolean).join(', ')}{a.pincode?' · '+a.pincode:''}</span><small>{a.contactName} · {a.contactPhone}</small></article>)}{!addresses.length&&<Empty title="No saved addresses" text="Add a home, office, or another city before creating a package." action="+ Add address" onClick={openNew}/>}</div></section></div>}
function Profile({form,setForm,save,profile,allergens}){return <div className="page"><section className="pageIntro"><div><span className="eyebrow">PERSONALISATION</span><h2>Health profile</h2><p>Set body metrics, goal, activity, dietary preferences and allergies.</p></div><button className="primary" onClick={save}>Save profile</button></section><div className="profileGrid"><section className="panel"><h3>Body & goal</h3><div className="twoCol"><label>Weight (kg)<input type="number" min="1" value={form.weightKg} onChange={e=>setForm({...form,weightKg:e.target.value})}/></label><label>Height (cm)<input type="number" min="1" value={form.heightCm} onChange={e=>setForm({...form,heightCm:e.target.value})}/></label></div><label>Date of birth<input type="date" value={form.dateOfBirth} onChange={e=>setForm({...form,dateOfBirth:e.target.value})}/></label><label>Primary goal<select value={form.goal} onChange={e=>setForm({...form,goal:e.target.value})}>{GOALS.map(x=><option key={x[0]} value={x[0]}>{x[1]}</option>)}</select></label><label>Activity level<select value={form.activityLevel} onChange={e=>setForm({...form,activityLevel:e.target.value})}>{ACTIVITY.map(x=><option key={x[0]} value={x[0]}>{x[1]}</option>)}</select></label>{profile?.bmi&&<div className="bmiCard"><span>Current BMI</span><b>{profile.bmi}</b><small>Calculated from saved height and weight.</small></div>}</section><section className="panel"><h3>Dietary preferences</h3><label>Diet<select value={form.diet} onChange={e=>setForm({...form,diet:e.target.value})}>{DIETS.map(x=><option key={x}>{x}</option>)}</select></label><div className="allergyChooser"><span className="fieldCaption">Allergies</span><p>Select from the HealthApp allergen master list. These choices are used for meal cautions.</p><div className="allergyOptions">{allergens.map(a=><label key={a.id} className={(form.allergyIds||[]).includes(a.id)?'allergyOption checked':'allergyOption'}><input type="checkbox" checked={(form.allergyIds||[]).includes(a.id)} onChange={e=>setForm({...form,allergyIds:e.target.checked?[...(form.allergyIds||[]),a.id]:(form.allergyIds||[]).filter(x=>x!==a.id)})}/><span>{a.name}</span></label>)}</div></div><div className="profileNotice"><b>Allergy caution</b><p>When a selected meal contains a matching recipe or ingredient allergen, HealthApp will ask for confirmation before it can be added to your package.</p></div></section></div></div>}
function Wallet({credit,transactions}){return <div className="page"><section className="walletHero"><div><span className="eyebrow">HEALTHAPP CREDIT</span><h2>Available credit</h2><p>Unused meal credit and refunds are recorded here when created by the backend.</p></div><strong>{money(credit.balance)}</strong></section><section className="panel"><div className="panelHead"><div><h3>Credit history</h3><p>Credits, debits, adjustments and refunds.</p></div></div><div className="creditTable">{transactions.map(x=><div className="creditRow" key={x.id}><div><b>{x.reason}</b><span>{new Date(x.createdAt).toLocaleString('en-IN')}</span></div><strong className={x.type==='Debit'?'negative':'positive'}>{x.type==='Debit'?'-':'+'}{money(x.amount)}</strong></div>)}{!transactions.length&&<Empty title="No credit activity" text="Credit transactions will appear here when recorded."/>}</div></section></div>}

function AddressModal({form,setForm,mode,cities,city,setCity,mapBusy,onMapPick,onSave,onClose}){const lat=Number(form.latitude),lng=Number(form.longitude);const selected=Number.isFinite(lat)&&Number.isFinite(lng)?[lat,lng]:cityMapCenter(city);const cityChange=value=>{setCity(value);setForm(f=>({...f,city:value,pincode:'',locality:'',cityAreaId:null,latitude:'',longitude:''}))};return <Modal title={mode==='new'?'Add delivery address':'Edit delivery address'} onClose={onClose}><div className="addressMapBlock"><div className="addressMapHint"><div><b>Pinpoint the exact delivery location</b><span>Choose {city}, then click anywhere at the exact delivery point. We will use this location to find outlets that can deliver.</span></div><strong>{Number.isFinite(lat)&&Number.isFinite(lng)?lat.toFixed(6)+', '+lng.toFixed(6):'Not pinned'}</strong></div><div className="addressMapFrame"><MapContainer center={selected} zoom={15} scrollWheelZoom className="addressMap"><TileLayer url={MAP_TILE_URL} attribution={MAP_ATTRIBUTION}/><MapRecenter center={selected}/><MapClickHandler onPick={onMapPick}/>{Number.isFinite(lat)&&Number.isFinite(lng)&&<CircleMarker center={selected} radius={9}/>}</MapContainer>{mapBusy&&<div className="mapBusyOverlay">Resolving address…</div>}</div><small className="mapAttributionHint">Exact coordinates are saved. Reverse lookup only fills editable address details.</small></div><div className="formGrid"><label>Delivery city<select value={city} onChange={e=>cityChange(e.target.value)}>{cities.map(x=><option key={x.city+'|'+x.state} value={x.city}>{x.city} · {x.state}</option>)}</select></label><label>Locality / Area<input value={form.locality||''} onChange={e=>setForm({...form,locality:e.target.value})} placeholder="Neighbourhood / locality"/></label><label>Pincode<input value={form.pincode||''} onChange={e=>setForm({...form,pincode:e.target.value})} placeholder="Auto-filled from map"/></label><label>Label<select value={form.label} onChange={e=>setForm({...form,label:e.target.value})}><option>Home</option><option>Office</option><option>Gym</option><option>Other</option></select></label><label>Contact name<input value={form.contactName} onChange={e=>setForm({...form,contactName:e.target.value})}/></label><label>Phone<input value={form.contactPhone} onChange={e=>setForm({...form,contactPhone:e.target.value})}/></label><label className="span2">Address line 1<input value={form.addressLine1} onChange={e=>setForm({...form,addressLine1:e.target.value})} placeholder="House / flat number and street"/></label><label className="span2">Address line 2<input value={form.addressLine2} onChange={e=>setForm({...form,addressLine2:e.target.value})} placeholder="Colony / street / landmark (optional)"/></label><div className="coordinateReadout span2"><span>Exact coordinates</span><b>{Number.isFinite(lat)&&Number.isFinite(lng)?lat.toFixed(6)+', '+lng.toFixed(6):'Pick a location on the map'}</b></div><label className="checkLabel span2"><input type="checkbox" checked={form.isDefault} onChange={e=>setForm({...form,isDefault:e.target.checked})}/> Set as default address</label></div><div className="modalActions"><button className="secondary" onClick={onClose}>Cancel</button><button className="primary" onClick={onSave}>Save address</button></div></Modal>}
function RescheduleModal({row,addresses,onClose,onChange,onSave}){return <Modal title={'Reschedule '+row.recipeName} onClose={onClose}><p>Unused meals can be moved within the subscription and up to seven days after its end date, subject to outlet menu availability.</p><div className="formGrid"><label>New date<input type="date" value={row.newDate||''} onChange={e=>onChange({...row,newDate:e.target.value})}/></label><label>Meal slot<select value={row.newSlot||row.mealSlot} onChange={e=>onChange({...row,newSlot:e.target.value})}>{SLOT.map(s=><option key={s.id} value={s.id}>{s.label}</option>)}</select></label><label className="span2">Delivery address<select value={row.addressId||''} onChange={e=>onChange({...row,addressId:e.target.value})}><option value="">Use original address</option>{addresses.map(a=><option key={a.id} value={a.id}>{a.label} · {a.areaName}</option>)}</select></label></div><div className="modalActions"><button className="secondary" onClick={onClose}>Cancel</button><button className="primary" onClick={onSave}>Reschedule meal</button></div></Modal>}

function MapRecenter({center}){const map=useMap();useEffect(()=>{map.setView(center,map.getZoom());requestAnimationFrame(()=>map.invalidateSize())},[center[0],center[1],map]);return null}
function MapClickHandler({onPick}){useMapEvents({click:e=>onPick(e.latlng.lat,e.latlng.lng)});return null}

function Modal({title,onClose,children}){return <div className="modalBackdrop" onMouseDown={e=>e.target===e.currentTarget&&onClose()}><div className="modal"><div className="modalHead"><h3>{title}</h3><button className="iconBtn" onClick={onClose}>×</button></div><div className="modalBody">{children}</div></div></div>}
function Empty({title,text,action,onClick}){return <div className="emptyState"><div className="emptyIcon">◎</div><h3>{title}</h3><p>{text}</p>{action&&<button className="primary" onClick={onClick}>{action}</button>}</div>}

createRoot(document.getElementById('root')).render(<App/>);
