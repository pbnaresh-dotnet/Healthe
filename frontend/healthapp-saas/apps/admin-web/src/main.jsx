import React,{useEffect,useMemo,useState}from'react';
import{createRoot}from'react-dom/client';
import{auth,admin,currentUser,money,API_URL,AppFeedbackProvider,useFeedback,AppAlert,AppModal}from'@healthapp/shared';
import'./styles.css';
import ApplicationErrorMonitor from './ApplicationErrorMonitor.jsx';
import OutletGroups from './OutletGroups.jsx';
import Outlet360 from './Outlet360.jsx';
import FinanceRulesHelp from './FinanceRulesHelp.jsx';
import SaaSBilling from './SaaSBilling.jsx';

const AREA_MANAGER_ROLE='AreaManager';

const NAV_GROUPS=[
 {label:'Command Center',items:[['overview','Dashboard','grid']]},
 {label:'Tenants',items:[['outlets','Outlets','building'],['area-managers','Area Managers','building'],['groups','Outlet Groups','building'],['onboarding','Onboarding','clipboard'],['domains','Domains','globe']]},
 {label:'Operations',items:[['geography','Cities & Coverage','pin']]},
 {label:'Finance & Reports',items:[['finance','Finance','chart'],['saas-billing','SaaS Billing & Invoices','chart'],['finance-rules','Finance Rules','clipboard']]},
 {label:'Platform',items:[['health','Platform Health','pulse']]}
];

function Icon({name,size=18}){
 const p={
  grid:<><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></>,
  building:<><path d="M4 21V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v17"/><path d="M16 8h4a1 1 0 0 1 1 1v12"/><path d="M8 7h4M8 11h4M8 15h4M8 19h4"/></>,
  clipboard:<><rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 4.5V3h6v1.5M8.5 9h7M8.5 13h7M8.5 17h4"/></>,
  globe:<><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/></>,
  pin:<><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/></>,
  chart:<><path d="M4 19V5M4 19h17"/><path d="m7 15 4-5 3 3 5-7"/></>,
  pulse:<><path d="M3 12h4l2-6 4 12 2-6h6"/></>,
  menu:<><path d="M4 7h16M4 12h16M4 17h16"/></>,
  chevron:<path d="m9 6 6 6-6 6"/>,
  arrow:<><path d="M5 12h14"/><path d="m13 6 6 6-6 6"/></>,
  refresh:<><path d="M20 11a8 8 0 1 0 2 5"/><path d="M20 4v7h-7"/></>,
  search:<><circle cx="11" cy="11" r="7"/><path d="m16.5 16.5 4 4"/></>,
  filter:<><path d="M4 6h16M7 12h10M10 18h4"/></>,
  close:<><path d="m6 6 12 12M18 6 6 18"/></>,
  check:<path d="m5 12 4 4L19 6"/>,
  warning:<><path d="M12 3 21 19H3L12 3Z"/><path d="M12 9v4M12 16h.01"/></>
 };
 return <svg className="icon" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{p[name]||p.grid}</svg>;
}

const formatDate=v=>v?new Date(v).toLocaleDateString('en-IN',{day:'2-digit',month:'short',year:'numeric'}):'—';
const formatDateTime=v=>v?new Date(v).toLocaleString('en-IN',{day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'}):'—';
const statusTone=s=>String(s||'').toLowerCase().includes('live')?'success':String(s||'').toLowerCase().includes('active')?'success':String(s||'').toLowerCase().includes('pending')?'warning':String(s||'').toLowerCase().includes('disabled')||String(s||'').toLowerCase().includes('rejected')?'danger':'neutral';
const roleLabel=s=>String(s||'').replace('OutletAdmin','Outlet Admin').replace('SuperAdmin','Super Admin').replace('OutletManager','Manager').replace('KitchenStaff','Kitchen Staff').replace('AreaManager','Area Manager');

function App(){
 const {notify}=useFeedback();
 const[u,setU]=useState(currentUser());
  const isAreaManager=String(u?.role||'').toLowerCase()==='areamanager';
 const[page,setPage]=useState('overview');
 const[sidebarOpen,setSidebarOpen]=useState(false);
 const[login,setLogin]=useState({email:'admin@healthapp.test',password:'demo'});
 const[data,setData]=useState({d:{},o:[],us:[],r:{},groups:[],areaManagers:[],managedOutlets:[],managerSummary:null});
  const[managerForm,setManagerForm]=useState({firstName:'',lastName:'',email:'',mobileNumber:'',password:'',outletIds:[]});
 const[domains,setDomains]=useState([]);
 const[outletPageData,setOutletPageData]=useState({items:[],totalCount:0,page:1,pageSize:25});
 const[outletPageNumber,setOutletPageNumber]=useState(1);
 const[outletPageSize,setOutletPageSize]=useState(25);
 const[outletPageLoading,setOutletPageLoading]=useState(false);
 const[cities,setCities]=useState([]);
 const[areas,setAreas]=useState([]);
 const[form,setForm]=useState({city:'Hyderabad',state:'Telangana',country:'India',latitude:17.385,longitude:78.4867,isEnabled:true});
 const[areaForm,setAreaForm]=useState({city:'Hyderabad',state:'Telangana',name:'',pincode:'',latitude:17.385,longitude:78.4867});
 const[error,setError]=useState('');
 const[loading,setLoading]=useState(false);
 const[selectedVerification,setSelectedVerification]=useState(null);
 const[verificationNotes,setVerificationNotes]=useState('');
 const[verification,setVerification]=useState([]);
 const[outletSearch,setOutletSearch]=useState('');
 const[selected360,setSelected360]=useState(null);
 const[outletStatus,setOutletStatus]=useState('');
  const[outletCity,setOutletCity]=useState('');
 const[domainStatus,setDomainStatus]=useState('');
 const[domainSearch,setDomainSearch]=useState('');
 const[financeFilters,setFinanceFilters]=useState(()=>{const d=new Date();d.setDate(d.getDate()-29);return {fromDate:d.toISOString().slice(0,10),toDate:new Date().toISOString().slice(0,10),outletGroupId:'',outletId:'',city:'',mealPlanId:''}});
 const[financeReport,setFinanceReport]=useState(null); const[financePolicy,setFinancePolicy]=useState(null);
 const[financeLoading,setFinanceLoading]=useState(false); const[financePageSize,setFinancePageSize]=useState(25); const[financeOutletSearch,setFinanceOutletSearch]=useState('');
 const fileUrl=url=>{if(!url)return'';return url.startsWith('http')?url:(API_URL?new URL(API_URL).origin+url:url)};
 const openPage=(next,filter='')=>{setPage(next);setSidebarOpen(false);if(next==='outlets'&&filter){setOutletStatus(filter);setOutletPageNumber(1)}};
 const reload=async()=>{
  setLoading(true);setError('');
  // Load each initial workspace dataset independently: one failed endpoint must
  // not discard successful results from the other admin sections.
  const requests=[
   ['dashboard',()=>admin.dashboard(),value=>setData(prev=>({...prev,d:value||{},o:value?.recentOutlets||[]}))],
   ['onboarding',()=>admin.outletOnboardingPending(),value=>setVerification(value||[])],
   ['domains',()=>admin.domains(),value=>setDomains(value||[])]
  ];
  try{
   const results=await Promise.allSettled(requests.map(([,request])=>request()));
   const failures=[];
   results.forEach((result,index)=>{
    const [name,,apply]=requests[index];
    if(result.status==='fulfilled')apply(result.value);
    else failures.push(`${name}: ${result.reason?.message||'request failed'}`);
   });
   if(failures.length)setError(`Some admin data could not be loaded. ${failures.join('; ')}. Other sections remain available; retry after checking Platform Health.`);
  }finally{setLoading(false)}
 };
 useEffect(()=>{if(u)reload()},[u]);
 useEffect(()=>{if(!u)return;let cancelled=false;const run=async()=>{try{
   if(page==='outlets'||page==='groups'){const gs=await admin.groups();if(!cancelled)setData(prev=>({...prev,groups:gs||[]}));}
   if(page==='overview'&&!isAreaManager){
     // Dashboard cards depend on these datasets; load them on entry rather than
     // showing misleading zero counts until the user visits another menu.
     const results=await Promise.allSettled([admin.revenue(),admin.cities()]);
     if(!cancelled){
       const [revenueResult,citiesResult]=results;
       if(revenueResult.status==='fulfilled')setData(prev=>({...prev,r:revenueResult.value||{}}));
       if(citiesResult.status==='fulfilled')setCities(citiesResult.value||[]);
       const failed=results.find(x=>x.status==='rejected');
       if(failed)setError(failed.reason?.message||'Some dashboard metrics could not be loaded.');
     }
   }
   if(page==='geography'){
    const results=await Promise.allSettled([admin.cities(),admin.cityAreas()]);
    if(!cancelled){
     const [cityResult,areaResult]=results;
     if(cityResult.status==='fulfilled')setCities(cityResult.value||[]);
     if(areaResult.status==='fulfilled')setAreas(areaResult.value||[]);
     const failed=results.find(x=>x.status==='rejected');
     if(failed)setError(failed.reason?.message||'Some coverage data could not be loaded.');
    }
   }
   if(page==='health'){const us=await admin.users();if(!cancelled)setData(prev=>({...prev,us:us||[]}));}
    if(['area-managers','domains','finance','health'].includes(page)){
    const requests=[page==='finance'?admin.outletsPage({page:1,pageSize:100}):admin.outlets()];
    if(page==='area-managers')requests.push(admin.areaManagers());
    if(page==='finance')requests.push(admin.groups());
    const results=await Promise.allSettled(requests);
    if(!cancelled){
     if(results[0].status==='fulfilled')setData(prev=>({...prev,o:page==='finance'?(results[0].value?.items||[]):(results[0].value||[])}));
     else setError(results[0].reason?.message||'Unable to load outlet lookup data.');
     if(page==='area-managers'&&results[1]){if(results[1].status==='fulfilled')setData(prev=>({...prev,areaManagers:results[1].value||[]}));else setError(results[1].reason?.message||'Unable to load Area Managers.');}
     if(page==='finance'&&results[1]){if(results[1].status==='fulfilled')setData(prev=>({...prev,groups:results[1].value||[]}));else setError(results[1].reason?.message||'Unable to load outlet groups.');}
    }
   }
    if(page==='manager-home'){
    const results=await Promise.allSettled([admin.areaManagerDashboard(),admin.myManagedOutlets()]);
    if(!cancelled){
     const [summaryResult,outletsResult]=results;
     if(summaryResult.status==='fulfilled')setData(prev=>({...prev,managerSummary:summaryResult.value||{}}));
     if(outletsResult.status==='fulfilled')setData(prev=>({...prev,managedOutlets:outletsResult.value||[]}));
     const failed=results.find(x=>x.status==='rejected');
     if(failed)setError(failed.reason?.message||'Some assigned-outlet data could not be loaded.');
    }
   }
  }catch(e){if(!cancelled)setError(e.message||'Unable to load this section')}};
  run();return()=>{cancelled=true};
 },[u,page]);
 useEffect(()=>{
  if(!u||page!=='outlets')return;
  let active=true;
  const timer=setTimeout(async()=>{
   setOutletPageLoading(true);
   try{
    const result=await admin.outletsPage({search:outletSearch,status:outletStatus,city:outletCity,page:outletPageNumber,pageSize:outletPageSize});
    if(active)setOutletPageData(result||{items:[],totalCount:0,page:outletPageNumber,pageSize:outletPageSize});
   }catch(e){if(active)setError(e.message||'Unable to load the outlet directory')}
   finally{if(active)setOutletPageLoading(false)}
  },250);
  return()=>{active=false;clearTimeout(timer)};
 },[u,page,outletSearch,outletStatus,outletCity,outletPageNumber,outletPageSize]);
 const loadFinance=async(filters=financeFilters,section='summary',pageNumber=1,pageSize=financePageSize)=>{
  try{
   setFinanceLoading(true);
   setError('');
   setFinancePageSize(pageSize);const normalized={...filters,fromDate:filters.fromDate||undefined,toDate:filters.toDate||undefined,outletGroupId:filters.outletGroupId||undefined,outletId:filters.outletId||undefined,city:filters.city||undefined,mealPlanId:filters.mealPlanId||undefined,section,page:pageNumber,pageSize};
   setFinanceReport(await admin.finance(normalized));
  }catch(e){setError(e.message||'Unable to load finance report')}finally{setFinanceLoading(false)}
 };
 useEffect(()=>{if(u&&page==='finance')loadFinance(financeFilters,'summary',1)},[u,page]);
 useEffect(()=>{if(!u||page!=='finance')return;let active=true;const timer=setTimeout(async()=>{try{const result=await admin.outletsPage({search:financeOutletSearch,page:1,pageSize:100});if(active)setData(prev=>({...prev,o:result?.items||[]}));}catch(e){if(active)setError(e.message||'Unable to search outlets for Finance.')}},250);return()=>{active=false;clearTimeout(timer)}},[u,page,financeOutletSearch]);
 const loadFinancePolicy=async()=>{try{setError('');setFinancePolicy(await admin.financePolicy())}catch(e){setError(e.message||'Unable to load finance policy')}};
 useEffect(()=>{if(u&&page==='finance-rules')loadFinancePolicy()},[u,page]);
 const sign=async e=>{e.preventDefault();try{setLoading(true);const x=await auth.login(login);setU(x.user);notify('Welcome back')}catch(e){setError(e.message||'Sign in failed')}finally{setLoading(false)}};

 const openProtectedDocument=async url=>{
  const token=localStorage.getItem('ha_token');
  if(!token){setError('Your admin session has expired. Please sign in again.');return}
  const tab=window.open('about:blank','_blank');
  if(!tab){setError('Please allow pop-ups to open protected documents.');return}
  const correlationId=globalThis.crypto?.randomUUID?.()||`${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  const startedAt=globalThis.performance?.now?.()??Date.now();
  try{
   setLoading(true);
   const response=await fetch(fileUrl(url),{headers:{Authorization:`Bearer ${token}`,'X-Correlation-Id':correlationId}});
   const durationMs=Math.round((globalThis.performance?.now?.()??Date.now())-startedAt);
   const responseCorrelationId=response.headers.get('X-Correlation-Id')||correlationId;
   if(import.meta.env.DEV&&(!response.ok||durationMs>=1000))console.warn('[HealthApp API]',{method:'GET',route:'protected-document',status:response.status,durationMs,correlationId:responseCorrelationId});
   if(!response.ok){const error=new Error('Unable to open the document.');error.correlationId=responseCorrelationId;throw error;}
   const blob=await response.blob(),objectUrl=URL.createObjectURL(blob);
   tab.location.href=objectUrl;setTimeout(()=>URL.revokeObjectURL(objectUrl),60000);
  }catch(e){if(import.meta.env.DEV)console.warn('[HealthApp API]',{method:'GET',route:'protected-document',status:e.status||0,durationMs:Math.round((globalThis.performance?.now?.()??Date.now())-startedAt),correlationId,error:'request-failed'});tab.close();setError((e.message||'Unable to open the document.')+(e.correlationId?' Reference: '+e.correlationId:''))}finally{setLoading(false)}
 };

 const createCity=async()=>{
  try{setLoading(true);await admin.createCity(form);await reload();notify(`${form.city} is now available`)}
  catch(e){setError(e.message||'Unable to save city')}finally{setLoading(false)}
 };
 const toggleCity=async city=>{
  try{setLoading(true);await admin.setCityEnabled(city.id,!city.isEnabled);await reload();notify(`${city.city} ${!city.isEnabled?'enabled':'disabled'}`)}
  catch(e){setError(e.message||'Unable to update city')}finally{setLoading(false)}
 };
 const createArea=async()=>{
  try{setLoading(true);await admin.createCityArea(areaForm);await reload();notify(`${areaForm.name||'Area'} added to ${areaForm.city}`);setAreaForm({...areaForm,name:'',pincode:''})}
  catch(e){setError(e.message||'Unable to add city area')}finally{setLoading(false)}
 };
 const review=async id=>{
  try{setLoading(true);setSelectedVerification(await admin.outletOnboardingDetail(id));setVerificationNotes('')}
  catch(e){setError(e.message||'Unable to load application')}finally{setLoading(false)}
 };
 const decide=async approve=>{
  if(!selectedVerification)return;
  if(!approve&&!verificationNotes.trim()){setError('Verification notes are required when rejecting an application.');return}
  try{
   setLoading(true);
   await admin.decideOutletOnboarding(selectedVerification.id,{approve,notes:verificationNotes});
   setSelectedVerification(null);await reload();notify(approve?'Outlet approved and activated':'Application rejected');
  }catch(e){setError(e.message||'Unable to update application')}finally{setLoading(false)}
 };
 const updateDomain=async(d,status)=>{
  try{setLoading(true);await admin.setDomainStatus(d.id,status);await reload();notify(status==='Active'?'Domain verified and activated':`Domain ${status.toLowerCase()}`)}
  catch(e){setError(e.message||'Unable to update domain')}finally{setLoading(false)}
 };

 const filteredOutlets=useMemo(()=>{
  const q=outletSearch.trim().toLowerCase();
  return (data.o||[]).filter(x=>{
   const hay=[x.name,x.slug,x.city,x.state,x.status,x.billingPlan].filter(Boolean).join(' ').toLowerCase();
   return (!q||hay.includes(q))&&(!outletStatus||String(x.status)===outletStatus)&&(!outletCity||String(x.city||'').toLowerCase()===outletCity.toLowerCase());
  });
 },[data.o,outletSearch,outletStatus,outletCity]);
 const filteredDomains=useMemo(()=>{
  const q=domainSearch.trim().toLowerCase();
  return (domains||[]).filter(x=>(!q||[x.hostname,x.outletName,x.type].filter(Boolean).join(' ').toLowerCase().includes(q))&&(!domainStatus||String(x.status)===domainStatus));
 },[domains,domainSearch,domainStatus]);
 const customerCount=Number(data.d?.customers??(data.us||[]).filter(x=>String(x.role||'').toLowerCase()==='customer').length);
 const adminCount=Number(data.d?.users??data.us?.length??0)-customerCount;
 const liveOutlets=Number(data.d?.liveOutlets??(data.o||[]).filter(x=>x.status==='Live').length);
 const activeWorkspaces=Number(data.d?.activeWorkspaces??(data.o||[]).filter(x=>x.status==='Active').length);
 const domainsActive=domains.filter(x=>x.status==='Active').length;
 const domainIssues=domains.filter(x=>x.status!=='Active').length;
 const revenue=data.r||{};
 const revenueRows=[
  ['Outlet SaaS revenue',revenue.outletSubscriptionRevenue||0],
  ['Customer service fees',revenue.customerServiceFeeRevenue||revenue.customerTransactionRevenue||0],
  ['Outlet commissions',revenue.outletCommissionRevenue||0],
  ['Late skip fees',revenue.lateSkipFeeRevenue||0]
 ];
 const attention=[
  {label:'Onboarding applications',count:verification.length,action:()=>openPage('onboarding')},
  {label:'Domains needing attention',count:domainIssues,action:()=>openPage('domains')},
  {label:'Outlets not yet Live',count:Math.max(0,Number(data.d?.outlets||0)-Number(data.d?.liveOutlets||0)),action:()=>openPage('outlets','Active')}
 ];

 if(!u)return <div className="auth"><form className="authCard" onSubmit={sign}><div className="brandMark"><span>H</span><div><b>HealthApp</b><small>Platform control</small></div></div><div><span className="eyebrow">SECURE ACCESS</span><h1>Super Admin</h1><p>Manage outlets, onboarding, domains, operations and platform health from one workspace.</p></div><label>Email<input autoComplete="username" value={login.email} onChange={e=>setLogin({...login,email:e.target.value})} placeholder="Admin email"/></label><label>Password<input autoComplete="current-password" type="password" value={login.password} onChange={e=>setLogin({...login,password:e.target.value})} placeholder="Password"/></label><button className="primaryBtn authSubmit" disabled={loading}>Sign in</button><div className="demoHint">Demo account <code>admin@healthapp.test</code> / <code>demo</code></div>{error&&<AppAlert type="error" message={error} onDismiss={()=>setError('')} className="errorBanner"/>}</form></div>;

 return <div className="adminApp">
  <header className="topbar">
   <button className="mobileMenuBtn" type="button" onClick={()=>setSidebarOpen(x=>!x)} aria-label="Open navigation"><Icon name="menu"/></button>
   <div className="topbarBrand"><div className="brandMini">H</div><div><b>HealthApp</b><span>Super Admin</span></div></div>
   <div className="topbarActions"><div className="adminIdentity"><span className="statusDot"></span><div><b>{u.email}</b><small>Super Admin</small></div></div><button type="button" className="ghostBtn" onClick={()=>{auth.logout();setU(null)}}>Sign out</button></div>
  </header>

  <div className={`layout ${sidebarOpen?'navOpen':''}`}>
   <aside className="sidebar">
    <div className="mobileSidebarHead"><b>Platform</b><button type="button" onClick={()=>setSidebarOpen(false)}><Icon name="close" size={16}/></button></div>
    <nav>{(isAreaManager?[{label:'My Area',items:[['manager-home','Assigned outlets','building'],['saas-billing','SaaS Billing','chart']]}]:NAV_GROUPS).map(group=><div className="navGroup" key={group.label}><span className="navGroupLabel">{group.label}</span>{group.items.map(([id,label,icon])=><button type="button" key={id} className={page===id?'navItem active':'navItem'} onClick={()=>openPage(id)}><Icon name={icon} size={17}/><span>{label}</span>{page===id&&<i/>}</button>)}</div>)}</nav>
    <div className="sidebarFooter"><span>Standalone SaaS</span><small>One database · tenant isolated</small></div>
   </aside>
   {sidebarOpen&&<button className="navScrim" aria-label="Close navigation" onClick={()=>setSidebarOpen(false)}></button>}

   <main className="content">
    {error&&<AppAlert type="error" message={error} onDismiss={()=>setError('')} className="errorBanner pageError"/>}
    {loading&&<div className="loadingBar"><span/></div>}

    {isAreaManager&&page==='overview'&&<AreaManagerWorkspace summary={data.managerSummary||{}} outlets={data.managedOutlets||[]} />}
     {page==='manager-home'&&isAreaManager&&<AreaManagerWorkspace summary={data.managerSummary||{}} outlets={data.managedOutlets||[]} />}
     {!isAreaManager&&page==='overview'&&<Dashboard openPage={openPage} data={data} cities={cities} domains={domains} verification={verification} filteredOutlets={filteredOutlets} customerCount={customerCount} liveOutlets={liveOutlets} activeWorkspaces={activeWorkspaces} domainsActive={domainsActive} domainIssues={domainIssues} revenue={revenue} attention={attention} revenueRows={revenueRows}/>}
    {!isAreaManager&&page==='outlets'&&<OutletDirectory outlets={outletPageData.items||[]} allOutlets={outletPageData.items||[]} total={outletPageData.totalCount||0} page={outletPageNumber} pageSize={outletPageSize} pageLoading={outletPageLoading} onPageChange={setOutletPageNumber} onPageSizeChange={size=>{setOutletPageSize(size);setOutletPageNumber(1)}} groups={data.groups||[]} search={outletSearch} setSearch={value=>{setOutletSearch(value);setOutletPageNumber(1)}} status={outletStatus} setStatus={value=>{setOutletStatus(value);setOutletPageNumber(1)}} city={outletCity} setCity={value=>{setOutletCity(value);setOutletPageNumber(1)}} loading={loading} onCreate={async payload=>{await admin.createOutlet(payload);await reload();const refreshed=await admin.outletsPage({search:outletSearch,status:outletStatus,city:outletCity,page:outletPageNumber,pageSize:outletPageSize});setOutletPageData(refreshed);notify('Outlet created and SaaS subscription assigned')}} onDashboard={()=>openPage('overview')} onOpen={x=>{setSelected360(x.id);openPage('outlet360')}} onAssign={async(outletId,groupId)=>{try{setLoading(true);await admin.assignOutletGroup(outletId,groupId||null);await reload();setOutletPageData(await admin.outletsPage({search:outletSearch,status:outletStatus,city:outletCity,page:outletPageNumber,pageSize:outletPageSize}));notify(groupId?'Outlet assigned to group':'Outlet removed from group')}catch(e){setError(e.message||'Unable to assign outlet group')}finally{setLoading(false)}}} />}
    {!isAreaManager&&page==='area-managers'&&<AreaManagers managers={data.areaManagers||[]} outlets={data.o||[]} form={managerForm} setForm={setManagerForm} loading={loading} onCreate={async()=>{try{setLoading(true);setError('');await admin.createAreaManager(managerForm);setManagerForm({firstName:'',lastName:'',email:'',mobileNumber:'',password:'',outletIds:[]});const managers=await admin.areaManagers();setData(prev=>({...prev,areaManagers:managers||[]}));notify('Area Manager created and outlets assigned')}catch(e){setError(e.message||'Unable to create Area Manager')}finally{setLoading(false)}}} onAssign={async(id,ids)=>{try{setLoading(true);await admin.assignAreaManagerOutlets(id,ids);const managers=await admin.areaManagers();setData(prev=>({...prev,areaManagers:managers||[]}));notify('Outlet assignments updated')}catch(e){setError(e.message||'Unable to update assignments')}finally{setLoading(false)}}} onStatus={async(id,active)=>{try{setLoading(true);await admin.setAreaManagerStatus(id,active);const managers=await admin.areaManagers();setData(prev=>({...prev,areaManagers:managers||[]}));notify(active?'Area Manager activated':'Area Manager deactivated')}catch(e){setError(e.message||'Unable to update status')}finally{setLoading(false)}}}/>}
     {!isAreaManager&&page==='groups'&&<OutletGroups groups={data.groups||[]} onSave={async(payload,id)=>{try{setLoading(true);if(id)await admin.updateGroup(id,payload);else await admin.createGroup(payload);await reload();notify(id?'Outlet group updated':'Outlet group created')}catch(e){setError(e.message||'Unable to save outlet group')}finally{setLoading(false)}}}/>} 
    {!isAreaManager&&page==='outlet360'&&<Outlet360 outletId={selected360} onBack={()=>openPage('outlets')}/>} 
    {!isAreaManager&&page==='onboarding'&&<Onboarding verification={verification} onReview={review} onDashboard={()=>openPage('overview')} />}
    {!isAreaManager&&page==='domains'&&<DomainCenter domains={filteredDomains} allDomains={domains} outlets={data.o||[]} search={domainSearch} setSearch={setDomainSearch} status={domainStatus} setStatus={setDomainStatus} onUpdate={updateDomain}/>}
    {!isAreaManager&&page==='geography'&&<Geography cities={cities} areas={areas} form={form} setForm={setForm} areaForm={areaForm} setAreaForm={setAreaForm} onCreateCity={createCity} onToggleCity={toggleCity} onCreateArea={createArea} loading={loading}/>}
    {!isAreaManager&&page==='finance'&&<Finance report={financeReport} filters={financeFilters} setFilters={setFinanceFilters} groups={data.groups||[]} outlets={data.o||[]} outletSearch={financeOutletSearch} setOutletSearch={setFinanceOutletSearch} onApply={loadFinance} loading={financeLoading} onFinanceRules={()=>openPage('finance-rules')}/>}
    {page==='saas-billing'&&<SaaSBilling isAreaManager={isAreaManager} onError={setError} notify={notify}/> }
    {!isAreaManager&&page==='finance-rules'&&<FinanceRulesHelp policy={financePolicy} onBack={()=>openPage('finance')} onFinance={()=>openPage('finance')}/>}
    {!isAreaManager&&page==='health'&&<section><PageIntro eyebrow="PLATFORM HEALTH" title="Operational health" text="Central visibility into application errors and tenant-impacting incidents. Drill into an event to see outlet, request, user and correlation context." action={<button className="secondaryBtn" onClick={()=>openPage('overview')}><Icon name="arrow" size={15}/> Command center</button>}/><ApplicationErrorMonitor outlets={data.o||[]}/></section>}
   </main>
  </div>

  {selectedVerification&&<VerificationModal item={selectedVerification} notes={verificationNotes} setNotes={setVerificationNotes} onClose={()=>setSelectedVerification(null)} onDocument={openProtectedDocument} onDecision={decide} loading={loading}/>}
  
 </div>;
}

function AreaManagerWorkspace({summary,outlets}){
 return <section>
  <PageIntro eyebrow="AREA MANAGER" title="Your assigned outlets" text="Your outlet portfolio and first point of contact workspace. You can see only outlets assigned to your account by Super Admin."/>
  <div className="metricGrid">
   <MetricCard label="Assigned outlets" value={summary.assignedOutlets??outlets.length} meta="Outlets in your area" tone="green"/>
   <MetricCard label="Live" value={summary.liveOutlets??0} meta="Serving customers" tone="blue"/>
   <MetricCard label="Pending" value={summary.pendingOutlets??0} meta="May need follow-up" tone="amber"/>
   <MetricCard label="Suspended" value={summary.suspendedOutlets??0} meta="Needs attention" tone="red"/>
  </div>
  <section className="card"><div className="cardHead"><div><span className="eyebrow">YOUR PORTFOLIO</span><h2>Outlet contacts</h2><p>Use this list to identify each outlet and its location. Assignments are controlled by Super Admin.</p></div></div>
   <div className="tenantTable"><div className="tableHead"><span>Outlet</span><span>City / State</span><span>Status</span><span>Assigned since</span></div>
    {outlets.map(o=><div className="tableRow" key={o.id}><div><b>{o.name}</b><small>{o.slug||'Outlet'}</small></div><span>{[o.city,o.state].filter(Boolean).join(', ')||'—'}</span><StatusPill status={o.status}/><span>{formatDate(o.assignedAtUtc)}</span></div>)}
    {!outlets.length&&<Empty text="No outlets are assigned to you yet. Contact Super Admin to have outlets assigned."/>}
   </div>
  </section>
 </section>;
}

function AreaManagers({managers,outlets,form,setForm,loading,onCreate,onAssign,onStatus}){
 const toggle=(id,checked)=>setForm(prev=>({...prev,outletIds:checked?[...new Set([...prev.outletIds,id])]:prev.outletIds.filter(x=>x!==id)}));
 return <section>
  <PageIntro eyebrow="PEOPLE & OWNERSHIP" title="Area Managers" text="Create a point of contact for outlets in a city or part of a city. Assign only the outlets this manager is responsible for."/>
  <div className="twoCol">
   <section className="card"><div className="cardHead"><div><span className="eyebrow">NEW ACCOUNT</span><h2>Create Area Manager</h2><p>Login credentials and outlet assignments are saved together.</p></div></div>
    <form className="formGrid" onSubmit={e=>{e.preventDefault();onCreate()}}>
     <label><span>First name</span><input required value={form.firstName} onChange={e=>setForm({...form,firstName:e.target.value})}/></label>
     <label><span>Last name</span><input required value={form.lastName} onChange={e=>setForm({...form,lastName:e.target.value})}/></label>
     <label><span>Email / login</span><input required type="email" value={form.email} onChange={e=>setForm({...form,email:e.target.value})}/></label>
     <label><span>Mobile number</span><input value={form.mobileNumber} onChange={e=>setForm({...form,mobileNumber:e.target.value})} placeholder="+91…"/></label>
     <label className="fullWidth"><span>Temporary password (minimum 12 characters)</span><input required minLength="12" type="password" value={form.password} onChange={e=>setForm({...form,password:e.target.value})}/></label>
     <div className="fullWidth"><b>Assign outlets</b><p className="muted">Select the outlets in this manager's city or half-city territory.</p>
      <div className="assignmentPicker">{outlets.map(o=><label key={o.id} className="assignmentOption"><input type="checkbox" checked={form.outletIds.includes(o.id)} onChange={e=>toggle(o.id,e.target.checked)}/><span><b>{o.name}</b><small>{[o.city,o.state].filter(Boolean).join(', ')||'Location not set'}</small></span></label>)}{!outlets.length&&<Empty text="No outlets exist to assign yet."/>}</div>
     </div>
     <button className="primaryBtn fullWidth" disabled={loading||!form.outletIds.length}>Create manager & assign {form.outletIds.length} outlet{form.outletIds.length===1?'':'s'}</button>
    </form>
   </section>
   <section className="card"><div className="cardHead"><div><span className="eyebrow">CURRENT COVERAGE</span><h2>Manager assignments</h2><p>Update territories as your outlet network grows.</p></div></div>
    <div className="managerList">{managers.map(m=><ManagerRow key={m.id} manager={m} outlets={outlets} loading={loading} onAssign={onAssign} onStatus={onStatus}/>)}{!managers.length&&<Empty text="No Area Managers have been created yet."/>}</div>
   </section>
  </div>
 </section>;
}
function ManagerRow({manager,outlets,loading,onAssign,onStatus}){
 const[editing,setEditing]=useState(false);const[selected,setSelected]=useState(manager.outletIds||[]);
 useEffect(()=>setSelected(manager.outletIds||[]),[manager.outletIds]);
 return <article className="managerRow"><div className="managerIdentity"><div className="managerAvatar">{(manager.firstName||'A').slice(0,1)}{(manager.lastName||'M').slice(0,1)}</div><div><b>{manager.firstName} {manager.lastName}</b><small>{manager.email} · {manager.mobileNumber||'No mobile'}</small><small>{(manager.outletNames||[]).join(', ')||'No outlets assigned'}</small></div><StatusPill status={manager.isActive?'Active':'Disabled'}/></div>
  {editing&&<div className="assignmentPicker">{outlets.map(o=><label key={o.id} className="assignmentOption"><input type="checkbox" checked={selected.includes(o.id)} onChange={e=>setSelected(prev=>e.target.checked?[...new Set([...prev,o.id])]:prev.filter(id=>id!==o.id))}/><span><b>{o.name}</b><small>{o.city}, {o.state}</small></span></label>)}</div>}
  <div className="managerActions"><button className="secondaryBtn" type="button" onClick={()=>editing?onAssign(manager.id,selected).then?.(()=>setEditing(false)):setEditing(true)}>{editing?'Save outlets':'Edit outlets'}</button><button className="secondaryBtn" type="button" disabled={loading} onClick={()=>onStatus(manager.id,!manager.isActive)}>{manager.isActive?'Deactivate':'Activate'}</button></div>
 </article>;
}

function PageIntro({eyebrow,title,text,action}){
 return <div className="pageIntro"><div><span className="eyebrow">{eyebrow}</span><h1>{title}</h1><p>{text}</p></div>{action&&<div className="pageIntroAction">{action}</div>}</div>;
}
function MetricCard({label,value,meta,tone='neutral',onClick}){
 return <button type="button" className={`metricCard metric-${tone}${onClick?' clickable':''}`} onClick={onClick}><div className="metricTop"><span>{label}</span>{onClick&&<Icon name="chevron" size={14}/>}</div><b>{value}</b>{meta&&<small>{meta}</small>}</button>;
}

function Dashboard({openPage,data,cities,domains,verification,customerCount,liveOutlets,activeWorkspaces,domainsActive,domainIssues,revenue,attention,revenueRows}){
 const recent=(data.d?.recentOutlets||data.o||[]).slice(0,6);
 const cityLive=cities.filter(x=>x.isEnabled).length;
 return <section>
  <PageIntro eyebrow="COMMAND CENTER" title="Platform overview" text="A single control tower for your standalone SaaS estate. Start with what needs attention, then drill into outlets, onboarding, domains, finance or health." action={<button className="primaryBtn" onClick={()=>openPage('outlets')}><Icon name="building" size={15}/> Manage outlets</button>}/>
  <div className="metricGrid">
   <MetricCard label="Total outlets" value={data.d?.outlets??data.o?.length??0} meta={`${liveOutlets} Live · ${activeWorkspaces} setup`} tone="green" onClick={()=>openPage('outlets')}/>
   <MetricCard label="Customers" value={customerCount} meta={`${data.d?.users??data.us?.length??0} total platform users`} tone="blue" onClick={()=>openPage('outlets')}/>
   <MetricCard label="Platform revenue" value={money(revenue.totalRevenue||0)} meta="Current recorded platform transactions" tone="purple" onClick={()=>openPage('finance')}/>
   <MetricCard label="Pending onboarding" value={verification.length} meta="Applications awaiting review" tone={verification.length?'amber':'green'} onClick={()=>openPage('onboarding')}/>
   <MetricCard label="Active domains" value={domainsActive} meta={`${domainIssues} need attention`} tone="teal" onClick={()=>openPage('domains')}/>
   <MetricCard label="Live cities" value={cityLive} meta={`${cities.length} configured on platform`} tone="slate" onClick={()=>openPage('geography')}/>
  </div>

  <div className="dashboardGrid">
   <section className="card attentionCard">
    <div className="cardHead"><div><span className="eyebrow">ACTION CENTER</span><h2>Needs attention</h2><p>Items that can block growth or tenant operations.</p></div><span className="miniLabel">Live</span></div>
    <div className="attentionList">{attention.map(x=><button type="button" key={x.label} className="attentionRow" onClick={x.action}><span className="attentionIcon">{x.count>0?<Icon name="warning" size={15}/>:<Icon name="check" size={15}/>}</span><span><b>{x.label}</b><small>{x.count>0?'Open the corresponding queue':'Nothing waiting right now'}</small></span><strong>{x.count}</strong><Icon name="chevron" size={14}/></button>)}</div>
   </section>

   <section className="card financeSummary">
    <div className="cardHead"><div><span className="eyebrow">FINANCE</span><h2>Revenue mix</h2><p>Platform-level recorded revenue, not outlet GST liability.</p></div><button className="linkBtn" onClick={()=>openPage('finance')}>Open finance <Icon name="arrow" size={13}/></button></div>
    <div className="revenueTotal"><span>Total recorded</span><b>{money(revenue.totalRevenue||0)}</b></div>
    <div className="revenueRows">{revenueRows.map(([label,value])=><div key={label}><span>{label}</span><b>{money(value)}</b></div>)}</div>
   </section>
  </div>

  <section className="card">
   <div className="cardHead"><div><span className="eyebrow">TENANT HEALTH</span><h2>Outlet estate</h2><p>Use this as your daily pulse before drilling into a tenant.</p></div><button className="linkBtn" onClick={()=>openPage('outlets')}>View all <Icon name="arrow" size={13}/></button></div>
   <div className="tenantTable">
    <div className="tableHead"><span>Outlet</span><span>Location</span><span>Status</span><span>Plan</span><span>Tenant</span><span>Action</span></div>
    {recent.map(x=><div className="tableRow" key={x.id}><div><b>{x.name||'Unnamed outlet'}</b><small>{x.slug}</small></div><span>{[x.city,x.state].filter(Boolean).join(', ')||'—'}</span><span><StatusPill status={x.status}/></span><span>{x.billingPlan||'—'}</span><span className="tenantCell">{x.subdomain||'Automatic tenant host'}</span><button type="button" className="rowAction" onClick={()=>openPage('outlets')}>Open <Icon name="arrow" size={12}/></button></div>)}
    {!recent.length&&<Empty text="No outlets have been configured yet."/>}
   </div>
  </section>

  <section className="quickLinks"><button onClick={()=>openPage('onboarding')}><Icon name="clipboard"/><span><b>Review onboarding</b><small>{verification.length} applications in queue</small></span><Icon name="arrow"/></button><button onClick={()=>openPage('domains')}><Icon name="globe"/><span><b>Manage domains</b><small>{domains.length} configured domains</small></span><Icon name="arrow"/></button><button onClick={()=>openPage('geography')}><Icon name="pin"/><span><b>Manage service geography</b><small>{cityLive} live cities</small></span><Icon name="arrow"/></button><button onClick={()=>openPage('health')}><Icon name="pulse"/><span><b>Open platform health</b><small>API and tenant-impacting errors</small></span><Icon name="arrow"/></button></section>
  <div className="phaseNote"><b>Designed for expansion:</b> the navigation is ready for Outlet Groups, Subscription Analytics, GST Reports, Payments, Audit Logs, Support and Saved Reports in the next phases without changing the tenant model.</div>
 </section>;
}

function StatusPill({status}){return <span className={`statusPill ${statusTone(status)}`}>{status||'Unknown'}</span>}
function Empty({text}){return <div className="emptyState">{text}</div>}

function Toolbar({children}){return <div className="toolbar">{children}</div>}

function OutletDirectory({outlets,allOutlets,total,page,pageSize,pageLoading,onPageChange,onPageSizeChange,groups,search,setSearch,status,setStatus,city,setCity,onDashboard,onOpen,onAssign,onCreate,loading}){
 const statuses=['Pending','Active','Suspended','Live'];
 const totalPages=Math.max(1,Math.ceil(total/Math.max(1,pageSize)));
 const [plans,setPlans]=useState([]);
 const [form,setForm]=useState({outletName:'',ownerFirstName:'',ownerLastName:'',email:'',mobileNumber:'',password:'',city:'',state:'',pincode:'',saasPlanId:'',billingCycle:'Monthly',discountPercent:'0',markAsPaid:false,paymentMethod:'Cash',paymentReference:'',paymentNotes:''});
 const [saving,setSaving]=useState(false);
 const [formError,setFormError]=useState('');
 useEffect(()=>{let active=true;admin.saasPlans().then(items=>{if(!active)return;setPlans(items||[]);setForm(prev=>({...prev,saasPlanId:prev.saasPlanId||(items||[])[0]?.id||''}));}).catch(e=>{if(active)setFormError(e.message||'Unable to load subscription plans')});return()=>{active=false}},[]);
 const update=(key,value)=>setForm(prev=>({...prev,[key]:value}));
 const selectedPlan=plans.find(p=>p.id===form.saasPlanId);
 const baseSubscription=selectedPlan?(form.billingCycle==='Annual'?Number(selectedPlan.annualFee||0):form.billingCycle==='SixMonths'?Number(selectedPlan.monthlyFee||0)*6*0.9:Number(selectedPlan.monthlyFee||0)):0;
 const setupFee=Number(selectedPlan?.setupFee??5000);
 const grossTotal=Math.round((baseSubscription+setupFee)*100)/100;
 const discount=Math.round(grossTotal*Math.min(100,Math.max(0,Number(form.discountPercent)||0))/100*100)/100;
 const amountDue=Math.max(0,Math.round((grossTotal-discount)*100)/100);
 const submit=async e=>{e.preventDefault();setFormError('');try{setSaving(true);await onCreate({...form,saasPlanId:form.saasPlanId,discountPercent:Number(form.discountPercent||0)});setForm({outletName:'',ownerFirstName:'',ownerLastName:'',email:'',mobileNumber:'',password:'',city:'',state:'',pincode:'',saasPlanId:plans[0]?.id||'',billingCycle:'Monthly',discountPercent:'0',markAsPaid:false,paymentMethod:'Cash',paymentReference:'',paymentNotes:''});}catch(e){setFormError(e.message||'Unable to create outlet')}finally{setSaving(false)}};
 return <section>
  <PageIntro eyebrow="TENANTS" title="Outlet directory" text="Create an outlet with its owner login and SaaS subscription, then manage the tenant directory and Outlet 360 workspace." action={<button className="secondaryBtn" onClick={onDashboard}><Icon name="arrow" size={15}/> Dashboard</button>}/>
  <section className="card createOutletCard"><div className="cardHead"><div><span className="eyebrow">NEW TENANT</span><h2>Add outlet and subscription</h2><p>Create the outlet and owner account together, apply a discount if needed, and optionally record the amount as paid.</p></div></div>
   <form onSubmit={submit}><div className="createOutletFields">
    <label>Outlet / business name<input required maxLength="160" value={form.outletName} onChange={e=>update('outletName',e.target.value)} placeholder="e.g. Green Bowl Kitchen"/></label>
    <label>Owner first name<input required maxLength="80" value={form.ownerFirstName} onChange={e=>update('ownerFirstName',e.target.value)} /></label>
    <label>Owner last name<input required maxLength="80" value={form.ownerLastName} onChange={e=>update('ownerLastName',e.target.value)} /></label>
    <label>Owner email<input required type="email" maxLength="254" value={form.email} onChange={e=>update('email',e.target.value)} /></label>
    <label>Mobile number<input type="tel" maxLength="30" value={form.mobileNumber} onChange={e=>update('mobileNumber',e.target.value)} /></label>
    <label>Temporary password<input required type="password" minLength="12" autoComplete="new-password" value={form.password} onChange={e=>update('password',e.target.value)} placeholder="At least 12 characters"/></label>
    <label>City<input required maxLength="100" value={form.city} onChange={e=>update('city',e.target.value)} placeholder="City"/></label>
    <label>State<input required maxLength="100" value={form.state} onChange={e=>update('state',e.target.value)} placeholder="State"/></label>
    <label>Pincode<input required maxLength="12" value={form.pincode} onChange={e=>update('pincode',e.target.value)} /></label>
    <label>SaaS subscription plan<select required value={form.saasPlanId} onChange={e=>update('saasPlanId',e.target.value)}><option value="">Select plan</option>{plans.map(p=><option key={p.id} value={p.id}>{p.name} · {money(p.monthlyFee)}/month · {money(p.annualFee)}/year</option>)}</select></label>
    <label>Billing cycle<select value={form.billingCycle} onChange={e=>update('billingCycle',e.target.value)}><option value="Monthly">Monthly</option><option value="SixMonths">6 months (10% discount)</option><option value="Annual">Annual</option></select></label>
    <label>Additional discount (%)<input type="number" min="0" max="100" step="0.01" value={form.discountPercent} onChange={e=>update('discountPercent',e.target.value)} /><small>Applied to setup fee + selected subscription amount.</small></label>
   </div>
   <div className="adminPaymentSummary"><div><span>Setup fee</span><b>{money(setupFee)}</b></div><div><span>Subscription</span><b>{money(baseSubscription)}</b></div><div><span>Gross total</span><b>{money(grossTotal)}</b></div><div><span>Discount</span><b>− {money(discount)}</b></div><div className="adminPaymentDue"><span>Net amount due</span><b>{money(amountDue)}</b></div></div>
   <section className="adminPaymentChoice"><label className="adminPaidToggle"><input type="checkbox" checked={form.markAsPaid} onChange={e=>update('markAsPaid',e.target.checked)}/><span><b>Mark amount as paid</b><small>Creates a payment record with the selected method. Use only after confirming receipt.</small></span></label>
    {form.markAsPaid&&<div className="createOutletFields adminPaymentFields"><label>Payment method<select required value={form.paymentMethod} onChange={e=>update('paymentMethod',e.target.value)}><option value="Cash">Cash</option><option value="UPI">UPI</option><option value="BankTransfer">Bank transfer</option><option value="Other">Other</option></select></label><label>Receipt / transaction reference{form.paymentMethod==='Cash'?<input value={form.paymentReference} onChange={e=>update('paymentReference',e.target.value)} placeholder="Optional cash receipt number"/>:<input required value={form.paymentReference} onChange={e=>update('paymentReference',e.target.value)} placeholder="Enter transaction/reference ID"/>}</label><label>Payment notes<input value={form.paymentNotes} onChange={e=>update('paymentNotes',e.target.value)} placeholder="Optional receipt or reconciliation note"/></label></div>}
   </section>
   {formError&&<div className="inlineError" role="alert">{formError}</div>}<div className="createOutletActions"><span>Discount and payment method are recorded for finance reconciliation. Mark paid only after the funds have actually been received.</span><button className="primaryBtn" type="submit" disabled={saving||loading||!plans.length}>{saving?'Creating outlet…':'Create outlet & assign plan'}</button></div></form>
  </section>
  <Toolbar><label className="searchField"><Icon name="search" size={16}/><input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search outlet, slug, city or state"/></label><label className="filterField"><Icon name="filter" size={15}/><select value={status} onChange={e=>setStatus(e.target.value)}><option value="">All statuses</option>{statuses.map(x=><option key={x}>{x}</option>)}</select></label><label className="filterField"><input value={city} onChange={e=>setCity(e.target.value)} placeholder="Filter city" aria-label="Filter by city"/></label><span className="resultCount">{outlets.length} of {total} outlets</span></Toolbar>
  <section className="card"><div className="mobileOnly sectionCount">{outlets.length} outlet{outlets.length===1?'':'s'}</div><div className="tenantTable outletTable"><div className="tableHead outletTableHead"><span>Outlet</span><span>Location</span><span>Status</span><span>Group</span><span>Plan</span><span>Action</span></div>{outlets.map(x=><div className="tableRow outletTableRow" key={x.id}><div><b>{x.name||'Unnamed outlet'}</b><small>{x.slug||'—'}</small>{x.subdomain?<a className="storefrontLink" href={`https://${x.subdomain}.broccoly.in`} target="_blank" rel="noreferrer">https://{x.subdomain}.broccoly.in ↗</a>:<small className="storefrontPending">Storefront subdomain assigned during outlet activation</small>}</div><span>{[x.city,x.state].filter(Boolean).join(', ')||'—'}</span><StatusPill status={x.status}/><label className="inlineGroupSelect"><select value={x.outletGroupId||''} onChange={e=>onAssign(x.id,e.target.value)}><option value="">Unassigned</option>{groups.filter(g=>g.isActive).map(g=><option key={g.id} value={g.id}>{g.name}</option>)}</select></label><span>{x.billingPlan||'—'}</span><button type="button" className="rowAction" onClick={()=>onOpen(x)}>360 <Icon name="arrow" size={12}/></button></div>)}{!outlets.length&&<Empty text={pageLoading?'Loading outlets…':'No outlets match your filters.'}/>}</div></section>
  <section className="card outletPagination"><div><span className="resultCount">Page {page} of {totalPages} · {total} matching outlets</span><label className="filterField">Rows <select value={pageSize} onChange={e=>onPageSizeChange(Number(e.target.value))}><option value={10}>10</option><option value={25}>25</option><option value={50}>50</option><option value={100}>100</option></select></label></div><div className="createOutletActions"><button type="button" className="secondaryBtn" disabled={page<=1||pageLoading} onClick={()=>onPageChange(page-1)}>Previous</button><button type="button" className="secondaryBtn" disabled={page>=totalPages||pageLoading} onClick={()=>onPageChange(page+1)}>Next</button></div></section>
  <div className="insightGrid"><InsightCard label="Live on this page" value={allOutlets.filter(x=>x.status==='Live').length} detail="Customer-facing outlets in current results" tone="green"/><InsightCard label="Grouped on this page" value={allOutlets.filter(x=>x.outletGroupId).length} detail="Current results assigned to groups" tone="blue"/><InsightCard label="Unassigned on this page" value={allOutlets.filter(x=>!x.outletGroupId).length} detail="Current results without a group" tone="amber"/></div>
 </section>;
}

function InsightCard({label,value,detail,tone='blue'}){return <div className={`insightCard ${tone}`}><span>{label}</span><b>{value}</b><small>{detail}</small></div>}

function Onboarding({verification,onReview,onDashboard}){
 const total=verification.length;
 return <section><PageIntro eyebrow="ONBOARDING" title="Outlet onboarding" text="Review submitted applications, inspect identity and business documents, then activate only verified tenants." action={<button className="secondaryBtn" onClick={onDashboard}><Icon name="arrow" size={15}/> Dashboard</button>}/><div className="metricGrid miniMetrics"><MetricCard label="Pending review" value={total} meta="Submitted onboarding applications" tone={total?'amber':'green'}/><MetricCard label="Workflow" value="Verify → Activate" meta="Payment and documents are checked before go-live" tone="blue"/></div><section className="card"><div className="cardHead"><div><span className="eyebrow">VERIFICATION QUEUE</span><h2>Applications waiting for review</h2><p>Demo and production applications use the same verification flow.</p></div><span className="countBadge">{total}</span></div><div className="applicationList">{verification.map(x=><article className="applicationCard" key={x.id}><div className="applicationMain"><div className="avatarCircle">{String(x.outletName||'O').trim().charAt(0).toUpperCase()}</div><div><h3>{x.outletName||'Unnamed outlet'}</h3><p>{x.ownerName||'Owner not supplied'} · {x.city||'City not supplied'} · {x.businessType||'Business type not supplied'}</p><small>Submitted {formatDateTime(x.submittedAtUtc)} · {x.planName||'Plan not selected'} · {x.paymentStatus||'Payment unknown'}</small></div></div><StatusPill status={x.status||'Pending'}/><button type="button" className="primaryBtn compactBtn" onClick={()=>onReview(x.id)}>Review application <Icon name="arrow" size={12}/></button></article>)}{!verification.length&&<Empty text="No applications are waiting for verification."/>}</div></section></section>;
}

function DomainCenter({domains,allDomains,outlets,search,setSearch,status,setStatus,onUpdate}){
 const statuses=[...new Set(allDomains.map(x=>x.status).filter(Boolean))].sort();
 const active=allDomains.filter(x=>x.status==='Active').length;
 const needing=allDomains.length-active;
 const platformOutlets=(outlets||[])
  .filter(x=>x.subdomain||x.slug)
  .map(x=>({...x,host:x.subdomain||x.slug}))
  .sort((a,b)=>String(a.host).localeCompare(String(b.host)));

 return (
  <section>
   <PageIntro
    eyebrow="DOMAINS"
    title="Tenant domain control"
    text="Manage automatic Broccoly storefront subdomains here. Wildcard DNS serves these hostnames without per-outlet DNS records; custom domains retain their separate verification lifecycle."
   />
   <section className="card" style={{marginBottom:16}}>
    <div className="cardHead">
     <div>
      <span className="eyebrow">BROCCOLY SUBDOMAINS</span>
      <h2>Automatic outlet storefronts</h2>
      <p>These hosts are generated from outlet records and do not need individual Cloudflare DNS records.</p>
     </div>
     <span className="resultCount">{platformOutlets.length} outlets</span>
    </div>
    <div className="domainList">
     {platformOutlets.map(o=>(
      <article className="domainCard" key={o.id}>
       <div className="domainIdentity">
        <div className="domainIcon"><Icon name="globe" size={18}/></div>
        <div>
         <h3>{o.host}.broccoly.in</h3>
         <p>{o.name||o.outletName||"Unnamed outlet"} · Automatic subdomain</p>
         <small>Outlet status: {o.status||"Unknown"}</small>
        </div>
       </div>
       <div className="domainActions">
        <a className="secondaryBtn compactBtn" href={"https://"+o.host+".broccoly.in"} target="_blank" rel="noreferrer">
         Open storefront <Icon name="arrow" size={12}/>
        </a>
       </div>
      </article>
     ))}
     {!platformOutlets.length&&<Empty text="No outlet subdomains have been assigned yet. Outlet subdomains are assigned during activation."/>}
    </div>
   </section>
   <div className="insightGrid domainStats">
    <InsightCard label="Configured" value={allDomains.length} detail="Custom tenant domains" tone="blue"/>
    <InsightCard label="Active" value={active} detail="Serving customer traffic" tone="green"/>
    <InsightCard label="Needs attention" value={needing} detail="Verification, activation or disabled state" tone={needing?'amber':'green'}/>
   </div>
   <Toolbar>
    <label className="searchField"><Icon name="search" size={16}/><input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search domain or outlet"/></label>
    <label className="filterField"><Icon name="filter" size={15}/><select value={status} onChange={e=>setStatus(e.target.value)}><option value="">All statuses</option>{statuses.map(x=><option key={x}>{x}</option>)}</select></label>
    <span className="resultCount">{domains.length} shown</span>
   </Toolbar>
   <section className="card">
    <div className="domainList">
     {domains.map(d=>(
      <article className="domainCard" key={d.id}>
       <div className="domainIdentity">
        <div className="domainIcon"><Icon name="globe" size={18}/></div>
        <div><h3>{d.hostname}</h3><p>{d.outletName||'Unknown outlet'} · {d.type||'Custom'}</p><small>Created {formatDate(d.createdAtUtc)} {d.isPrimary?'· Primary':''}</small></div>
       </div>
       <div className="domainState"><StatusPill status={d.status}/>{d.type==='Custom'&&d.status!=='Active'&&<small>TXT: <code>{d.verificationName||'Not available'}</code></small>}</div>
       <div className="domainActions">{d.type==='Custom'&&d.status!=='Active'&&<button type="button" className="primaryBtn compactBtn" onClick={()=>onUpdate(d,'Active')}>Verify & activate</button>}{d.type==='Custom'&&d.status==='Active'&&<button type="button" className="secondaryBtn compactBtn" onClick={()=>onUpdate(d,'Disabled')}>Disable</button>}</div>
      </article>
     ))}
     {!domains.length&&<Empty text="No custom domains match your filters."/>}
    </div>
   </section>
  </section>
 );
}
function Geography({cities,areas,form,setForm,areaForm,setAreaForm,onCreateCity,onToggleCity,onCreateArea,loading}){
 const [areaCity,setAreaCity]=useState('');
 const filteredAreas=areaCity?areas.filter(x=>String(x.city).toLowerCase()===areaCity.toLowerCase()):areas;
 const cityNames=[...new Set(cities.map(x=>x.city).filter(Boolean))].sort();
 return <section><PageIntro eyebrow="OPERATIONS" title="Cities & coverage" text="Super Admin owns the platform geography. Outlets then select the areas they can serve and configure their own radius and pricing."/><div className="geographyGrid"><section className="card"><div className="cardHead"><div><span className="eyebrow">SERVICE CITIES</span><h2>Live platform cities</h2><p>Customer location checks can only resolve against enabled cities.</p></div></div><div className="cityCards">{cities.map(city=><article className={`cityCard ${city.isEnabled?'enabled':''}`} key={city.id}><div className="cityMarker"><Icon name="pin" size={17}/></div><div><h3>{city.city}</h3><p>{city.state} · {city.country}</p></div><StatusPill status={city.isEnabled?'Live':'Disabled'}/><button className="secondaryBtn compactBtn" type="button" disabled={loading} onClick={()=>onToggleCity(city)}>{city.isEnabled?'Disable':'Enable'}</button></article>)}{!cities.length&&<Empty text="No service cities configured."/>}</div></section><section className="card"><div className="cardHead"><div><span className="eyebrow">LAUNCH CITY</span><h2>Add service city</h2><p>Latitude and longitude set the initial map centre.</p></div></div><div className="formGrid responsive3"><label>City<input value={form.city} onChange={e=>setForm({...form,city:e.target.value})}/></label><label>State<input value={form.state} onChange={e=>setForm({...form,state:e.target.value})}/></label><label>Country<input value={form.country} onChange={e=>setForm({...form,country:e.target.value})}/></label><label>Latitude<input type="number" step="0.000001" value={form.latitude} onChange={e=>setForm({...form,latitude:Number(e.target.value)})}/></label><label>Longitude<input type="number" step="0.000001" value={form.longitude} onChange={e=>setForm({...form,longitude:Number(e.target.value)})}/></label><label className="checkLine"><input type="checkbox" checked={form.isEnabled} onChange={e=>setForm({...form,isEnabled:e.target.checked})}/> Enable immediately</label></div><button className="primaryBtn" type="button" onClick={onCreateCity} disabled={loading}><Icon name="pin" size={15}/> Save city</button></section></div><section className="card"><div className="cardHead"><div><span className="eyebrow">CITY AREAS</span><h2>Platform delivery areas</h2><p>Keep the area master here; outlet-specific coverage stays inside the outlet workspace.</p></div><span className="countBadge">{areas.length}</span></div><Toolbar><label className="filterField"><Icon name="filter" size={15}/><select value={areaCity} onChange={e=>setAreaCity(e.target.value)}><option value="">All cities</option>{cityNames.map(x=><option key={x}>{x}</option>)}</select></label><span className="resultCount">{filteredAreas.length} areas</span></Toolbar><div className="areaTable"><div className="tableHead"><span>Area</span><span>City</span><span>Pincode</span><span>Status</span></div>{filteredAreas.map(a=><div className="tableRow" key={a.id}><div><b>{a.name||'Unnamed area'}</b><small>{a.latitude?.toFixed?.(5)}, {a.longitude?.toFixed?.(5)}</small></div><span>{a.city}, {a.state}</span><span>{a.pincode||'—'}</span><StatusPill status={a.isActive?'Active':'Disabled'}/></div>)}{!filteredAreas.length&&<Empty text="No city areas match the selected city."/>}</div><div className="areaCreate"><h3>Add city area</h3><div className="formGrid responsive4"><label>City<input value={areaForm.city} onChange={e=>setAreaForm({...areaForm,city:e.target.value})}/></label><label>State<input value={areaForm.state} onChange={e=>setAreaForm({...areaForm,state:e.target.value})}/></label><label>Area name<input value={areaForm.name} onChange={e=>setAreaForm({...areaForm,name:e.target.value})}/></label><label>Pincode<input value={areaForm.pincode} onChange={e=>setAreaForm({...areaForm,pincode:e.target.value})}/></label><label>Latitude<input type="number" step="0.000001" value={areaForm.latitude} onChange={e=>setAreaForm({...areaForm,latitude:Number(e.target.value)})}/></label><label>Longitude<input type="number" step="0.000001" value={areaForm.longitude} onChange={e=>setAreaForm({...areaForm,longitude:Number(e.target.value)})}/></label></div><button className="secondaryBtn" type="button" onClick={onCreateArea} disabled={loading}><Icon name="pin" size={15}/> Add area</button></div></section></section>;
}

function FinancePager({report,filters,section,onApply}){
 if(!report||report.section!==section)return null;
 const totalPages=Math.max(1,Math.ceil((report.totalRows||0)/(report.pageSize||25)));
 return <div className="paginationBar"><span>{report.totalRows||0} rows · Page {report.page||1} of {totalPages}</span><div><label className="pageSizeField">Rows <select value={report.pageSize||25} onChange={e=>onApply(filters,section,1,Number(e.target.value))}><option value="10">10</option><option value="25">25</option><option value="50">50</option><option value="100">100</option></select></label><button type="button" className="secondaryBtn compactBtn" disabled={(report.page||1)<=1} onClick={()=>onApply(filters,section,(report.page||1)-1,report.pageSize||25)}>Previous</button><button type="button" className="secondaryBtn compactBtn" disabled={(report.page||1)>=totalPages} onClick={()=>onApply(filters,section,(report.page||1)+1,report.pageSize||25)}>Next</button></div></div>;
}
function Finance({report,filters,setFilters,groups,outlets,outletSearch,setOutletSearch,onApply,loading,onFinanceRules}){
 const[tab,setTab]=useState('summary');
 const totals=report?.totals;
 const paid=totals?.paidSubscriptions;
 const all=totals?.allSubscriptions;
 const moneyValue=v=>money(v||0);
 const set=(key,value)=>setFilters(x=>({...x,[key]:value}));
 return <section className="financeControl">
  <PageIntro eyebrow="FINANCE & GST CONTROL CENTER" title="Financial command center" text="Review customer collections, outlet settlements, restaurant GST and HealthApp fees from the tenant subscription ledger. Filters stay tenant-aware and financial responsibilities remain separate." action={<div className="financePageActions"><button className="secondaryBtn" onClick={onFinanceRules}><Icon name="clipboard" size={15}/>Rules & help</button><button className="secondaryBtn" onClick={()=>onApply(filters,tab,1)} disabled={loading}><Icon name="refresh" size={15}/>{loading?'Refreshing…':'Refresh report'}</button></div>}/>
  <section className="card financeFilterCard">
   <div className="cardHead"><div><span className="eyebrow">REPORT FILTERS</span><h2>Control the reporting window</h2><p>Dates use subscription start dates for cohort and daily subscription reporting.</p></div></div>
   <div className="financeFilters">
    <label><span>From</span><input type="date" value={filters.fromDate||''} onChange={e=>set('fromDate',e.target.value)}/></label>
    <label><span>To</span><input type="date" value={filters.toDate||''} onChange={e=>set('toDate',e.target.value)}/></label>
    <label><span>Outlet group</span><select value={filters.outletGroupId||''} onChange={e=>set('outletGroupId',e.target.value)}><option value="">All groups</option>{groups.map(g=><option key={g.id} value={g.id}>{g.name}</option>)}</select></label>
    <label><span>Find outlet</span><input value={outletSearch} onChange={e=>setOutletSearch(e.target.value)} placeholder="Search outlet name, city or slug"/></label><label><span>Outlet</span><select value={filters.outletId||''} onChange={e=>set('outletId',e.target.value)}><option value="">All outlets</option>{outlets.map(o=><option key={o.id} value={o.id}>{o.name}</option>)}</select></label>
    <label><span>City</span><input value={filters.city||''} onChange={e=>set('city',e.target.value)} placeholder="All cities"/></label>
    <div className="financeFilterActions"><button className="primaryBtn" onClick={()=>onApply(filters,tab,1)} disabled={loading}>{loading?'Running report…':'Apply filters'}</button><button className="secondaryBtn" onClick={()=>{const next={fromDate:filters.fromDate,toDate:filters.toDate,outletGroupId:'',outletId:'',city:'',mealPlanId:''};setFilters(next);onApply(next,tab,1)}} disabled={loading}>Clear scope</button></div>
   </div>
  </section>
  {!report?<section className="card financeEmpty"><b>{loading?'Building the finance report…':'No finance report loaded'}</b><span>{loading?'Querying the tenant subscription ledger.':'Choose the reporting window and apply the filters.'}</span></section>:
  <>
   <div className="financeScopeBanner"><div><span className="eyebrow">REPORTING WINDOW</span><b>{formatDate(report.fromDate)} → {formatDate(report.toDate)}</b></div><div><span>Subscriptions</span><strong>{totals.subscriptionCount}</strong></div><div><span>Paid</span><strong>{totals.paidSubscriptionCount}</strong></div><div><span>Pending payment</span><strong>{totals.pendingSubscriptionCount}</strong></div></div>
   <div className="metricGrid financeMetricGrid">
    <MetricCard label="Customer charges collected" value={moneyValue(paid?.customerCharges)} meta="Paid subscriptions only" tone="green"/>
    <MetricCard label="Restaurant taxable" value={moneyValue(paid?.restaurantTaxableAmount)} meta="Outlet tax base" tone="blue"/>
    <MetricCard label="Restaurant GST" value={moneyValue(paid?.restaurantGstAmount)} meta="Outlet liability · not HealthApp revenue" tone="amber"/>
    <MetricCard label="Platform service fee" value={moneyValue(paid?.platformServiceFee)} meta="HealthApp fee before GST" tone="purple"/>
    <MetricCard label="Platform service GST" value={moneyValue(paid?.platformServiceGst)} meta="GST on HealthApp service fee" tone="teal"/>
    <MetricCard label="Outlet settlement" value={moneyValue(paid?.outletSettlementAmount)} meta="Recorded outlet amount" tone="slate"/>
    <MetricCard label="HealthApp revenue" value={moneyValue(paid?.healthAppRevenue)} meta="Service fee + commission + late-skip fee" tone="green"/>
    <MetricCard label="Pending customer value" value={moneyValue((all?.customerCharges||0)-(paid?.customerCharges||0))} meta="Not counted in paid metrics" tone="amber"/>
   </div>
   <div className="financeBoundary"><Icon name="check" size={16}/><div><b>Tax boundary is explicit</b><span>Restaurant GST belongs to the outlet's tax reporting. Platform service GST is reported separately. Neither is silently added to HealthApp revenue.</span></div></div>
   <div className="financeTabs">{[['summary','Summary'],['groups','Outlet groups'],['outlets','Outlets'],['daily','Daily subscriptions']].map(([id,label])=><button key={id} className={tab===id?'active':''} onClick={()=>{setTab(id);onApply(filters,id,1)}}>{label}</button>)}</div>
   {tab==='summary'&&<div className="financePanelGrid">
    <section className="card"><div className="cardHead"><div><span className="eyebrow">LEDGER VIEW</span><h2>Gross vs paid</h2></div></div><div className="financeLedgerRows"><div><span>Gross meal amount</span><b>{moneyValue(all?.grossMealAmount)}</b></div><div><span>Discounts</span><b>- {moneyValue(all?.discountAmount)}</b></div><div><span>Net meals</span><b>{moneyValue(all?.netMealAmount)}</b></div><div><span>Delivery fees</span><b>{moneyValue(all?.deliveryFee)}</b></div><div><span>Customer charges</span><b>{moneyValue(all?.customerCharges)}</b></div><div><span>Restaurant GST</span><b>{moneyValue(all?.restaurantGstAmount)}</b></div><div><span>Platform fees + commission</span><b>{moneyValue(all?.platformServiceFee + all?.outletCommissionAmount)}</b></div><div><span>Outlet settlement</span><b>{moneyValue(all?.outletSettlementAmount)}</b></div></div></section>
    <section className="card"><div className="cardHead"><div><span className="eyebrow">REPORTING NOTE</span><h2>Refunds & reconciliation</h2><p>The current ledger does not contain a proper immutable refund transaction model, so this report does not invent refund values.</p></div></div><div className="financeReconciliation"><span className="statusPill warning">Refund ledger pending</span><p>Payment reconciliation and refund accounting will be added as the next finance slice using provider payment records and explicit refund transactions.</p></div></section>
   </div>}
   {tab==='groups'&&<section className="card"><div className="cardHead"><div><span className="eyebrow">GROUP ECONOMICS</span><h2>Finance by outlet group</h2><p>Unassigned outlets remain visible so tenant coverage gaps are not hidden.</p></div></div><div className="financeTableWrap"><table className="dataTable financeTable"><thead><tr><th>Group</th><th>Outlets</th><th>Subscriptions</th><th>Paid</th><th>Charges</th><th>Restaurant GST</th><th>Platform fee</th><th>HealthApp revenue</th></tr></thead><tbody>{report.groups.map(x=><tr key={x.outletGroupId||'unassigned'}><td><b>{x.groupName}</b></td><td>{x.outletCount}</td><td>{x.subscriptionCount}</td><td>{x.paidSubscriptionCount}</td><td>{moneyValue(x.amounts.customerCharges)}</td><td>{moneyValue(x.amounts.restaurantGstAmount)}</td><td>{moneyValue(x.amounts.platformServiceFee)}</td><td><b>{moneyValue(x.amounts.healthAppRevenue)}</b></td></tr>)}{!report.groups.length&&<tr><td colSpan="8"><Empty text="No group rows for this period."/></td></tr>}</tbody></table></div><FinancePager report={report} filters={filters} section="groups" onApply={onApply}/></section>}
   {tab==='outlets'&&<section className="card"><div className="cardHead"><div><span className="eyebrow">TENANT ECONOMICS</span><h2>Outlet finance</h2><p>Compare customer volume and tax/fee flows without exposing data across tenants.</p></div></div><div className="financeTableWrap"><table className="dataTable financeTable"><thead><tr><th>Outlet</th><th>City</th><th>Group</th><th>Subscriptions</th><th>Paid</th><th>Charges</th><th>Restaurant GST</th><th>Settlement</th><th>HealthApp</th></tr></thead><tbody>{report.outlets.map(x=><tr key={x.outletId}><td><b>{x.outletName}</b></td><td>{x.city||'—'}</td><td>{x.groupName}</td><td>{x.subscriptionCount}</td><td>{x.paidSubscriptionCount}</td><td>{moneyValue(x.amounts.customerCharges)}</td><td>{moneyValue(x.amounts.restaurantGstAmount)}</td><td>{moneyValue(x.amounts.outletSettlementAmount)}</td><td><b>{moneyValue(x.amounts.healthAppRevenue)}</b></td></tr>)}{!report.outlets.length&&<tr><td colSpan="9"><Empty text="No outlet rows for this period."/></td></tr>}</tbody></table></div><FinancePager report={report} filters={filters} section="outlets" onApply={onApply}/></section>}
   {tab==='daily'&&<section className="card"><div className="cardHead"><div><span className="eyebrow">DAILY SUBSCRIPTIONS</span><h2>Subscriptions by day and group</h2><p>Use this view for daily operational and finance reconciliation before adding exports.</p></div></div><div className="financeTableWrap"><table className="dataTable financeTable"><thead><tr><th>Date</th><th>Group</th><th>Subscriptions</th><th>Paid</th><th>Charges</th><th>Restaurant taxable</th><th>Restaurant GST</th><th>Platform fee</th><th>HealthApp</th></tr></thead><tbody>{report.daily.map(x=><tr key={x.date+'-'+(x.outletGroupId||'unassigned')}><td><b>{formatDate(x.date)}</b></td><td>{x.groupName}</td><td>{x.subscriptionCount}</td><td>{x.paidSubscriptionCount}</td><td>{moneyValue(x.amounts.customerCharges)}</td><td>{moneyValue(x.amounts.restaurantTaxableAmount)}</td><td>{moneyValue(x.amounts.restaurantGstAmount)}</td><td>{moneyValue(x.amounts.platformServiceFee)}</td><td><b>{moneyValue(x.amounts.healthAppRevenue)}</b></td></tr>)}{!report.daily.length&&<tr><td colSpan="9"><Empty text="No daily rows for this period."/></td></tr>}</tbody></table></div><FinancePager report={report} filters={filters} section="daily" onApply={onApply}/></section>}
  </>}
 </section>;
}

function VerificationModal({item,notes,setNotes,onClose,onDocument,onDecision,loading}){
 const docs=[['Aadhaar card',item.aadhaarCardUrl],['Business registration',item.businessRegistrationUrl],['Business PAN',item.businessPanDocumentUrl],['GST certificate',item.gstCertificateUrl]].filter(([,url])=>url);
 return <AppModal title="Outlet application verification" onClose={onClose} showHeader={false} backdropClass="modalBackdrop" modalClass="modalShell verificationShell" bodyClass={null}><div className="modalHead"><div><span className="eyebrow">OUTLET APPLICATION</span><h2>{item.outletName||'Unnamed outlet'}</h2><p>{item.planName||'Plan'} · {item.billingCycle||'Billing cycle'} · {item.paymentStatus||'Payment'}</p></div><button type="button" className="iconBtn" onClick={onClose}><Icon name="close"/></button></div><div className="verifyGrid"><section><h3>Business details</h3><div className="facts"><span>Type</span><b>{item.businessType||'—'}</b><span>Location</span><b>{[item.city,item.state,item.pincode].filter(Boolean).join(', ')||'—'}</b><span>Address</span><b>{[item.addressLine1,item.addressLine2].filter(Boolean).join(', ')||'—'}</b><span>Description</span><b>{item.description||'—'}</b></div></section><section><h3>Owner & identity</h3><div className="facts"><span>Owner</span><b>{item.ownerName||'—'}</b><span>Email</span><b>{item.ownerEmail||'—'}</b><span>Phone</span><b>{item.ownerPhone||'—'}</b><span>Aadhaar</span><b>XXXX XXXX {String(item.aadhaarNumber||'').slice(-4)||'—'}</b><span>Business PAN</span><b>{item.businessPan||'—'}</b><span>GST</span><b>{item.gstNumber||'—'}</b></div></section><section className="verifyDocs"><h3>Uploaded documents</h3><div className="docGrid">{docs.map(([label,url])=><button type="button" className="docRow" key={label} onClick={()=>onDocument(url)}><span className="docCheck"><Icon name="check" size={12}/></span><span><b>{label}</b><small>Open protected document</small></span><Icon name="arrow" size={14}/></button>)}{!docs.length&&<Empty text="No protected documents were uploaded."/>}</div></section><section className="decisionBox"><h3>Verification decision</h3><textarea value={notes} onChange={e=>setNotes(e.target.value)} placeholder="Add verification notes. Notes are required when rejecting."/><div className="verifyActions"><button type="button" className="secondaryBtn dangerBtn" disabled={loading||!notes.trim()} onClick={()=>onDecision(false)}>Reject</button><button type="button" className="primaryBtn" disabled={loading} onClick={()=>onDecision(true)}>Approve & activate <Icon name="check" size={13}/></button></div></section></div></AppModal>;
}

export default function EmbeddedAdminApp(){return <AppFeedbackProvider><App/></AppFeedbackProvider>;}
