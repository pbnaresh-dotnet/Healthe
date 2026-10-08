import React,{useEffect,useMemo,useState}from'react';
import{createRoot}from'react-dom/client';
import{auth,admin,currentUser,money,API_URL}from'@healthapp/shared';
import'./styles.css';
import ApplicationErrorMonitor from './ApplicationErrorMonitor.jsx';
import OutletGroups from './OutletGroups.jsx';
import Outlet360 from './Outlet360.jsx';

const NAV_GROUPS=[
 {label:'Command Center',items:[['overview','Dashboard','grid']]},
 {label:'Tenants',items:[['outlets','Outlets','building'],['groups','Outlet Groups','building'],['onboarding','Onboarding','clipboard'],['domains','Domains','globe']]},
 {label:'Operations',items:[['geography','Cities & Coverage','pin']]},
 {label:'Finance & Reports',items:[['finance','Finance','chart']]},
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
const roleLabel=s=>String(s||'').replace('OutletAdmin','Outlet Admin').replace('SuperAdmin','Super Admin').replace('OutletManager','Manager').replace('KitchenStaff','Kitchen Staff');

function App(){
 const[u,setU]=useState(currentUser());
 const[page,setPage]=useState('overview');
 const[sidebarOpen,setSidebarOpen]=useState(false);
 const[login,setLogin]=useState({email:'admin@healthapp.test',password:'demo'});
 const[data,setData]=useState({d:{},o:[],us:[],r:{},groups:[]});
 const[domains,setDomains]=useState([]);
 const[cities,setCities]=useState([]);
 const[areas,setAreas]=useState([]);
 const[form,setForm]=useState({city:'Hyderabad',state:'Telangana',country:'India',latitude:17.385,longitude:78.4867,isEnabled:true});
 const[areaForm,setAreaForm]=useState({city:'Hyderabad',state:'Telangana',name:'',pincode:'',latitude:17.385,longitude:78.4867});
 const[error,setError]=useState('');
 const[toast,setToast]=useState('');
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
 const notify=m=>{setToast(m);setTimeout(()=>setToast(''),2600)};
 const fileUrl=url=>{if(!url)return'';return url.startsWith('http')?url:(API_URL?new URL(API_URL).origin+url:url)};
 const openPage=(next,filter='')=>{setPage(next);setSidebarOpen(false);if(next==='outlets'&&filter)setOutletStatus(filter)};
 const reload=async()=>{
  try{
   setLoading(true);setError('');
   const[d,o,us,r,cs,v,ds,ars,gs]=await Promise.all([
    admin.dashboard(),admin.outlets(),admin.users(),admin.revenue(),admin.cities(),admin.outletOnboardingPending(),admin.domains(),admin.cityAreas(),admin.groups()
   ]);
   setData({d:d||{},o:o||[],us:us||[],r:r||{},groups:gs||[]});setCities(cs||[]);setVerification(v||[]);setDomains(ds||[]);setAreas(ars||[]);
  }catch(e){setError(e.message||'Unable to load Super Admin data.')}finally{setLoading(false)}
 };
 useEffect(()=>{if(u)reload()},[u]);
 const sign=async e=>{e.preventDefault();try{setLoading(true);const x=await auth.login(login);setU(x.user);notify('Welcome back')}catch(e){setError(e.message||'Sign in failed')}finally{setLoading(false)}};

 const openProtectedDocument=async url=>{
  const token=localStorage.getItem('ha_token');
  if(!token){setError('Your admin session has expired. Please sign in again.');return}
  const tab=window.open('about:blank','_blank');
  if(!tab){setError('Please allow pop-ups to open protected documents.');return}
  try{
   setLoading(true);
   const response=await fetch(fileUrl(url),{headers:{Authorization:`Bearer ${token}` }});
   if(!response.ok)throw new Error('Unable to open the document.');
   const blob=await response.blob(),objectUrl=URL.createObjectURL(blob);
   tab.location.href=objectUrl;setTimeout(()=>URL.revokeObjectURL(objectUrl),60000);
  }catch(e){tab.close();setError(e.message||'Unable to open the document.')}finally{setLoading(false)}
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
 const customerCount=(data.us||[]).filter(x=>String(x.role||'').toLowerCase()==='customer').length;
 const adminCount=(data.us||[]).filter(x=>String(x.role||'').toLowerCase()!=='customer').length;
 const liveOutlets=(data.o||[]).filter(x=>x.status==='Live').length;
 const activeWorkspaces=(data.o||[]).filter(x=>x.status==='Active').length;
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
  {label:'Outlets not yet Live',count:(data.o||[]).filter(x=>x.status!=='Live').length,action:()=>openPage('outlets','Active')}
 ];

 if(!u)return <div className="auth"><form className="authCard" onSubmit={sign}><div className="brandMark"><span>H</span><div><b>HealthApp</b><small>Platform control</small></div></div><div><span className="eyebrow">SECURE ACCESS</span><h1>Super Admin</h1><p>Manage outlets, onboarding, domains, operations and platform health from one workspace.</p></div><label>Email<input autoComplete="username" value={login.email} onChange={e=>setLogin({...login,email:e.target.value})} placeholder="Admin email"/></label><label>Password<input autoComplete="current-password" type="password" value={login.password} onChange={e=>setLogin({...login,password:e.target.value})} placeholder="Password"/></label><button className="primaryBtn authSubmit" disabled={loading}>Sign in</button><div className="demoHint">Demo account <code>admin@healthapp.test</code> / <code>demo</code></div>{error&&<div className="errorBanner"><span>{error}</span><button type="button" onClick={()=>setError('')}><Icon name="close" size={15}/></button></div>}</form></div>;

 return <div className="adminApp">
  <header className="topbar">
   <button className="mobileMenuBtn" type="button" onClick={()=>setSidebarOpen(x=>!x)} aria-label="Open navigation"><Icon name="menu"/></button>
   <div className="topbarBrand"><div className="brandMini">H</div><div><b>HealthApp</b><span>Super Admin</span></div></div>
   <div className="topbarActions"><div className="adminIdentity"><span className="statusDot"></span><div><b>{u.email}</b><small>Super Admin</small></div></div><button type="button" className="ghostBtn" onClick={()=>{auth.logout();setU(null)}}>Sign out</button></div>
  </header>

  <div className={`layout ${sidebarOpen?'navOpen':''}`}>
   <aside className="sidebar">
    <div className="mobileSidebarHead"><b>Platform</b><button type="button" onClick={()=>setSidebarOpen(false)}><Icon name="close" size={16}/></button></div>
    <nav>{NAV_GROUPS.map(group=><div className="navGroup" key={group.label}><span className="navGroupLabel">{group.label}</span>{group.items.map(([id,label,icon])=><button type="button" key={id} className={page===id?'navItem active':'navItem'} onClick={()=>openPage(id)}><Icon name={icon} size={17}/><span>{label}</span>{page===id&&<i/>}</button>)}</div>)}</nav>
    <div className="sidebarFooter"><span>Standalone SaaS</span><small>One database · tenant isolated</small></div>
   </aside>
   {sidebarOpen&&<button className="navScrim" aria-label="Close navigation" onClick={()=>setSidebarOpen(false)}></button>}

   <main className="content">
    {error&&<div className="errorBanner pageError"><span>{error}</span><button type="button" onClick={()=>setError('')}><Icon name="close" size={15}/></button></div>}
    {loading&&<div className="loadingBar"><span/></div>}

    {page==='overview'&&<Dashboard openPage={openPage} data={data} cities={cities} domains={domains} verification={verification} filteredOutlets={filteredOutlets} customerCount={customerCount} liveOutlets={liveOutlets} activeWorkspaces={activeWorkspaces} domainsActive={domainsActive} domainIssues={domainIssues} revenue={revenue} attention={attention} revenueRows={revenueRows}/>}
    {page==='outlets'&&<OutletDirectory outlets={filteredOutlets} allOutlets={data.o||[]} groups={data.groups||[]} search={outletSearch} setSearch={setOutletSearch} status={outletStatus} setStatus={setOutletStatus} city={outletCity} setCity={setOutletCity} onDashboard={()=>openPage('overview')} onOpen={x=>{setSelected360(x.id);openPage('outlet360')}} onAssign={async(outletId,groupId)=>{try{setLoading(true);await admin.assignOutletGroup(outletId,groupId||null);await reload();notify(groupId?'Outlet assigned to group':'Outlet removed from group')}catch(e){setError(e.message||'Unable to assign outlet group')}finally{setLoading(false)}}} />}
    {page==='groups'&&<OutletGroups groups={data.groups||[]} onSave={async(payload,id)=>{try{setLoading(true);if(id)await admin.updateGroup(id,payload);else await admin.createGroup(payload);await reload();notify(id?'Outlet group updated':'Outlet group created')}catch(e){setError(e.message||'Unable to save outlet group')}finally{setLoading(false)}}}/>} 
    {page==='outlet360'&&<Outlet360 outletId={selected360} onBack={()=>openPage('outlets')}/>} 
    {page==='onboarding'&&<Onboarding verification={verification} onReview={review} onDashboard={()=>openPage('overview')} />}
    {page==='domains'&&<DomainCenter domains={filteredDomains} allDomains={domains} search={domainSearch} setSearch={setDomainSearch} status={domainStatus} setStatus={setDomainStatus} onUpdate={updateDomain}/>}
    {page==='geography'&&<Geography cities={cities} areas={areas} form={form} setForm={setForm} areaForm={areaForm} setAreaForm={setAreaForm} onCreateCity={createCity} onToggleCity={toggleCity} onCreateArea={createArea} loading={loading}/>}
    {page==='finance'&&<Finance revenue={revenue} rows={revenueRows} outletCount={data.o?.length||0}/>}
    {page==='health'&&<section><PageIntro eyebrow="PLATFORM HEALTH" title="Operational health" text="Central visibility into application errors and tenant-impacting incidents. Drill into an event to see outlet, request, user and correlation context." action={<button className="secondaryBtn" onClick={()=>openPage('overview')}><Icon name="arrow" size={15}/> Command center</button>}/><ApplicationErrorMonitor outlets={data.o||[]}/></section>}
   </main>
  </div>

  {selectedOutlet&&<OutletQuickView outlet={selectedOutlet} onClose={()=>setSelectedOutlet(null)} onOutlets={()=>{setSelectedOutlet(null);openPage('outlets')}} />}
  {selectedVerification&&<VerificationModal item={selectedVerification} notes={verificationNotes} setNotes={setVerificationNotes} onClose={()=>setSelectedVerification(null)} onDocument={openProtectedDocument} onDecision={decide} loading={loading}/>}
  {toast&&<div className="toast"><Icon name="check" size={15}/>{toast}</div>}
 </div>;
}

function PageIntro({eyebrow,title,text,action}){
 return <div className="pageIntro"><div><span className="eyebrow">{eyebrow}</span><h1>{title}</h1><p>{text}</p></div>{action&&<div className="pageIntroAction">{action}</div>}</div>;
}
function MetricCard({label,value,meta,tone='neutral',onClick}){
 return <button type="button" className={`metricCard metric-${tone}${onClick?' clickable':''}`} onClick={onClick}><div className="metricTop"><span>{label}</span>{onClick&&<Icon name="chevron" size={14}/>}</div><b>{value}</b>{meta&&<small>{meta}</small>}</button>;
}

function Dashboard({openPage,data,cities,domains,verification,customerCount,liveOutlets,activeWorkspaces,domainsActive,domainIssues,revenue,attention,revenueRows}){
 const recent=(data.o||[]).slice(0,6);
 const cityLive=cities.filter(x=>x.isEnabled).length;
 return <section>
  <PageIntro eyebrow="COMMAND CENTER" title="Platform overview" text="A single control tower for your standalone SaaS estate. Start with what needs attention, then drill into outlets, onboarding, domains, finance or health." action={<button className="primaryBtn" onClick={()=>openPage('outlets')}><Icon name="building" size={15}/> Manage outlets</button>}/>
  <div className="metricGrid">
   <MetricCard label="Total outlets" value={data.o?.length||0} meta={`${liveOutlets} Live · ${activeWorkspaces} setup`} tone="green" onClick={()=>openPage('outlets')}/>
   <MetricCard label="Customers" value={customerCount} meta={`${data.us?.length||0} total platform users`} tone="blue" onClick={()=>openPage('outlets')}/>
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

function OutletDirectory({outlets,allOutlets,search,setSearch,status,setStatus,city,setCity,onDashboard,onOpen}){
 const cities=[...new Set(allOutlets.map(x=>x.city).filter(Boolean))].sort();
 const statuses=[...new Set(allOutlets.map(x=>x.status).filter(Boolean))].sort();
 return <section>
  <PageIntro eyebrow="TENANTS" title="Outlet directory" text="Find a tenant quickly, review its status and use the directory as the entry point for support and reporting." action={<button className="secondaryBtn" onClick={onDashboard}><Icon name="arrow" size={15}/> Dashboard</button>}/>
  <Toolbar><label className="searchField"><Icon name="search" size={16}/><input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search outlet, slug, city or state"/></label><label className="filterField"><Icon name="filter" size={15}/><select value={status} onChange={e=>setStatus(e.target.value)}><option value="">All statuses</option>{statuses.map(x=><option key={x}>{x}</option>)}</select></label><label className="filterField"><select value={city} onChange={e=>setCity(e.target.value)}><option value="">All cities</option>{cities.map(x=><option key={x}>{x}</option>)}</select></label><span className="resultCount">{outlets.length} of {allOutlets.length}</span></Toolbar>
  <section className="card"><div className="mobileOnly sectionCount">{outlets.length} outlet{outlets.length===1?'':'s'}</div><div className="tenantTable outletTable"><div className="tableHead"><span>Outlet</span><span>Location</span><span>Status</span><span>Plan</span><span>Slug / host</span><span>Action</span></div>{outlets.map(x=><div className="tableRow" key={x.id}><div><b>{x.name||'Unnamed outlet'}</b><small>{x.about||'No business description'}</small></div><span>{[x.city,x.state,x.pincode].filter(Boolean).join(', ')||'—'}</span><StatusPill status={x.status}/><span>{x.billingPlan||'—'}</span><span className="tenantCell"><b>{x.slug||'—'}</b><small>{x.subdomain||'No subdomain'}</small></span><button type="button" className="rowAction" onClick={()=>onOpen(x)}>Open <Icon name="arrow" size={12}/></button></div>)}{!outlets.length&&<Empty text="No outlets match your filters."/>}</div></section>
  <div className="insightGrid"><InsightCard label="Live outlets" value={allOutlets.filter(x=>x.status==='Live').length} detail="Customer-facing tenant workspaces" tone="green"/><InsightCard label="Setup / active" value={allOutlets.filter(x=>x.status!=='Live').length} detail="Tenants that still need go-live work" tone="amber"/><InsightCard label="Cities represented" value={new Set(allOutlets.map(x=>x.city).filter(Boolean)).size} detail="Distinct outlet operating locations" tone="blue"/></div>
 </section>;
}

function InsightCard({label,value,detail,tone='blue'}){return <div className={`insightCard ${tone}`}><span>{label}</span><b>{value}</b><small>{detail}</small></div>}

function Onboarding({verification,onReview,onDashboard}){
 const total=verification.length;
 return <section><PageIntro eyebrow="ONBOARDING" title="Outlet onboarding" text="Review submitted applications, inspect identity and business documents, then activate only verified tenants." action={<button className="secondaryBtn" onClick={onDashboard}><Icon name="arrow" size={15}/> Dashboard</button>}/><div className="metricGrid miniMetrics"><MetricCard label="Pending review" value={total} meta="Submitted onboarding applications" tone={total?'amber':'green'}/><MetricCard label="Workflow" value="Verify → Activate" meta="Payment and documents are checked before go-live" tone="blue"/></div><section className="card"><div className="cardHead"><div><span className="eyebrow">VERIFICATION QUEUE</span><h2>Applications waiting for review</h2><p>Demo and production applications use the same verification flow.</p></div><span className="countBadge">{total}</span></div><div className="applicationList">{verification.map(x=><article className="applicationCard" key={x.id}><div className="applicationMain"><div className="avatarCircle">{String(x.outletName||'O').trim().charAt(0).toUpperCase()}</div><div><h3>{x.outletName||'Unnamed outlet'}</h3><p>{x.ownerName||'Owner not supplied'} · {x.city||'City not supplied'} · {x.businessType||'Business type not supplied'}</p><small>Submitted {formatDateTime(x.submittedAtUtc)} · {x.planName||'Plan not selected'} · {x.paymentStatus||'Payment unknown'}</small></div></div><StatusPill status={x.status||'Pending'}/><button type="button" className="primaryBtn compactBtn" onClick={()=>onReview(x.id)}>Review application <Icon name="arrow" size={12}/></button></article>)}{!verification.length&&<Empty text="No applications are waiting for verification."/>}</div></section></section>;
}

function DomainCenter({domains,allDomains,search,setSearch,status,setStatus,onUpdate}){
 const statuses=[...new Set(allDomains.map(x=>x.status).filter(Boolean))].sort();
 const active=allDomains.filter(x=>x.status==='Active').length;
 const needing=allDomains.length-active;
 return <section><PageIntro eyebrow="DOMAINS" title="Tenant domain control" text="Keep custom domains in a predictable lifecycle: requested → verified → active. Activation remains gated by the outlet lifecycle and Cloudflare validation."/><div className="insightGrid domainStats"><InsightCard label="Configured" value={allDomains.length} detail="Tenant domains" tone="blue"/><InsightCard label="Active" value={active} detail="Serving customer traffic" tone="green"/><InsightCard label="Needs attention" value={needing} detail="Verification, activation or disabled state" tone={needing?'amber':'green'}/></div><Toolbar><label className="searchField"><Icon name="search" size={16}/><input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search domain or outlet"/></label><label className="filterField"><Icon name="filter" size={15}/><select value={status} onChange={e=>setStatus(e.target.value)}><option value="">All statuses</option>{statuses.map(x=><option key={x}>{x}</option>)}</select></label><span className="resultCount">{domains.length} shown</span></Toolbar><section className="card"><div className="domainList">{domains.map(d=><article className="domainCard" key={d.id}><div className="domainIdentity"><div className="domainIcon"><Icon name="globe" size={18}/></div><div><h3>{d.hostname}</h3><p>{d.outletName||'Unknown outlet'} · {d.type||'Custom'}</p><small>Created {formatDate(d.createdAtUtc)} {d.isPrimary?'· Primary':''}</small></div></div><div className="domainState"><StatusPill status={d.status}/>{d.type==='Custom'&&d.status!=='Active'&&<small>TXT: <code>{d.verificationName||'Not available'}</code></small>}</div><div className="domainActions">{d.type==='Custom'&&d.status!=='Active'&&<button type="button" className="primaryBtn compactBtn" onClick={()=>onUpdate(d,'Active')}>Verify & activate</button>}{d.type==='Custom'&&d.status==='Active'&&<button type="button" className="secondaryBtn compactBtn" onClick={()=>onUpdate(d,'Disabled')}>Disable</button>}</div></article>)}{!domains.length&&<Empty text="No domains match your filters."/>}</div></section></section>;
}

function Geography({cities,areas,form,setForm,areaForm,setAreaForm,onCreateCity,onToggleCity,onCreateArea,loading}){
 const [areaCity,setAreaCity]=useState('');
 const filteredAreas=areaCity?areas.filter(x=>String(x.city).toLowerCase()===areaCity.toLowerCase()):areas;
 const cityNames=[...new Set(cities.map(x=>x.city).filter(Boolean))].sort();
 return <section><PageIntro eyebrow="OPERATIONS" title="Cities & coverage" text="Super Admin owns the platform geography. Outlets then select the areas they can serve and configure their own radius and pricing."/><div className="geographyGrid"><section className="card"><div className="cardHead"><div><span className="eyebrow">SERVICE CITIES</span><h2>Live platform cities</h2><p>Customer location checks can only resolve against enabled cities.</p></div></div><div className="cityCards">{cities.map(city=><article className={`cityCard ${city.isEnabled?'enabled':''}`} key={city.id}><div className="cityMarker"><Icon name="pin" size={17}/></div><div><h3>{city.city}</h3><p>{city.state} · {city.country}</p></div><StatusPill status={city.isEnabled?'Live':'Disabled'}/><button className="secondaryBtn compactBtn" type="button" disabled={loading} onClick={()=>onToggleCity(city)}>{city.isEnabled?'Disable':'Enable'}</button></article>)}{!cities.length&&<Empty text="No service cities configured."/>}</div></section><section className="card"><div className="cardHead"><div><span className="eyebrow">LAUNCH CITY</span><h2>Add service city</h2><p>Latitude and longitude set the initial map centre.</p></div></div><div className="formGrid responsive3"><label>City<input value={form.city} onChange={e=>setForm({...form,city:e.target.value})}/></label><label>State<input value={form.state} onChange={e=>setForm({...form,state:e.target.value})}/></label><label>Country<input value={form.country} onChange={e=>setForm({...form,country:e.target.value})}/></label><label>Latitude<input type="number" step="0.000001" value={form.latitude} onChange={e=>setForm({...form,latitude:Number(e.target.value)})}/></label><label>Longitude<input type="number" step="0.000001" value={form.longitude} onChange={e=>setForm({...form,longitude:Number(e.target.value)})}/></label><label className="checkLine"><input type="checkbox" checked={form.isEnabled} onChange={e=>setForm({...form,isEnabled:e.target.checked})}/> Enable immediately</label></div><button className="primaryBtn" type="button" onClick={onCreateCity} disabled={loading}><Icon name="pin" size={15}/> Save city</button></section></div><section className="card"><div className="cardHead"><div><span className="eyebrow">CITY AREAS</span><h2>Platform delivery areas</h2><p>Keep the area master here; outlet-specific coverage stays inside the outlet workspace.</p></div><span className="countBadge">{areas.length}</span></div><Toolbar><label className="filterField"><Icon name="filter" size={15}/><select value={areaCity} onChange={e=>setAreaCity(e.target.value)}><option value="">All cities</option>{cityNames.map(x=><option key={x}>{x}</option>)}</select></label><span className="resultCount">{filteredAreas.length} areas</span></Toolbar><div className="areaTable"><div className="tableHead"><span>Area</span><span>City</span><span>Pincode</span><span>Status</span></div>{filteredAreas.map(a=><div className="tableRow" key={a.id}><div><b>{a.name||'Unnamed area'}</b><small>{a.latitude?.toFixed?.(5)}, {a.longitude?.toFixed?.(5)}</small></div><span>{a.city}, {a.state}</span><span>{a.pincode||'—'}</span><StatusPill status={a.isActive?'Active':'Disabled'}/></div>)}{!filteredAreas.length&&<Empty text="No city areas match the selected city."/>}</div><div className="areaCreate"><h3>Add city area</h3><div className="formGrid responsive4"><label>City<input value={areaForm.city} onChange={e=>setAreaForm({...areaForm,city:e.target.value})}/></label><label>State<input value={areaForm.state} onChange={e=>setAreaForm({...areaForm,state:e.target.value})}/></label><label>Area name<input value={areaForm.name} onChange={e=>setAreaForm({...areaForm,name:e.target.value})}/></label><label>Pincode<input value={areaForm.pincode} onChange={e=>setAreaForm({...areaForm,pincode:e.target.value})}/></label><label>Latitude<input type="number" step="0.000001" value={areaForm.latitude} onChange={e=>setAreaForm({...areaForm,latitude:Number(e.target.value)})}/></label><label>Longitude<input type="number" step="0.000001" value={areaForm.longitude} onChange={e=>setAreaForm({...areaForm,longitude:Number(e.target.value)})}/></label></div><button className="secondaryBtn" type="button" onClick={onCreateArea} disabled={loading}><Icon name="pin" size={15}/> Add area</button></div></section></section>;
}

function Finance({revenue,rows,outletCount}){
 return <section><PageIntro eyebrow="FINANCE & REPORTS" title="Platform finance" text="This first release surfaces the revenue records already available through the platform transaction layer. GST, settlement and outlet-group reports can build on the same dimensions in the reporting phase."/><div className="financeHero"><div><span>Total recorded platform revenue</span><b>{money(revenue.totalRevenue||0)}</b><small>Across {outletCount} outlet{outletCount===1?'':'s'} in the current transaction source</small></div><div className="financeChip"><span>Reporting boundary</span><b>HealthApp revenue</b><small>Outlet tax liability remains separate</small></div></div><div className="financeCards">{rows.map(([label,value])=><div className="financeCard" key={label}><span>{label}</span><b>{money(value)}</b><small>Recorded by platform transaction type</small></div>)}</div><section className="card reportRoadmap"><div className="cardHead"><div><span className="eyebrow">REPORTING FOUNDATION</span><h2>Production reporting model</h2><p>The shell is ready for the next reporting modules without redesigning the workspace.</p></div></div><div className="roadmapGrid"><div><b>Daily subscriptions</b><span>By date → outlet group → outlet → meal plan.</span></div><div><b>GST & tax</b><span>Gross, discount, taxable, restaurant GST, platform fee and platform GST.</span></div><div><b>Payments</b><span>Provider, payment status, failure reason and reconciliation.</span></div><div><b>Outlet economics</b><span>Sales, fees, GST, outlet amount and HealthApp revenue.</span></div></div></section></section>;
}

function OutletQuickView({outlet,onClose,onOutlets}){ 
 const fields=[
  ['Status',outlet.status||'—'],
  ['SaaS plan',outlet.billingPlan||'—'],
  ['City',outlet.city||'—'],
  ['State',outlet.state||'—'],
  ['Pincode',outlet.pincode||'—'],
  ['Tenant slug',outlet.slug||'—'],
  ['Subdomain',outlet.subdomain||'—']
 ];
 return <div className="modalBackdrop quickViewBackdrop"><div className="modalShell quickViewShell"><div className="modalHead"><div><span className="eyebrow">TENANT QUICK VIEW</span><h2>{outlet.name||'Unnamed outlet'}</h2><p>Tenant-safe snapshot. Detailed Outlet 360 controls will be added in the tenant-management phase.</p></div><button type="button" className="iconBtn" onClick={onClose}><Icon name="close"/></button></div><div className="quickViewBanner"><div className="avatarCircle">{String(outlet.name||'O').trim().charAt(0).toUpperCase()}</div><div><b>{outlet.slug||'No tenant slug'}</b><small>{outlet.city||'No city'} · {outlet.status||'Unknown status'}</small></div><StatusPill status={outlet.status}/></div><div className="facts quickFacts">{fields.map(([label,value])=><React.Fragment key={label}><span>{label}</span><b>{value}</b></React.Fragment>)}</div><div className="quickViewActions"><button type="button" className="secondaryBtn" onClick={onClose}>Close</button><button type="button" className="primaryBtn" onClick={onOutlets}>Back to outlet directory <Icon name="arrow" size={13}/></button></div></div></div>;
}

function VerificationModal({item,notes,setNotes,onClose,onDocument,onDecision,loading}){
 const docs=[['Aadhaar card',item.aadhaarCardUrl],['Business registration',item.businessRegistrationUrl],['Business PAN',item.businessPanDocumentUrl],['GST certificate',item.gstCertificateUrl]].filter(([,url])=>url);
 return <div className="modalBackdrop"><div className="modalShell verificationShell"><div className="modalHead"><div><span className="eyebrow">OUTLET APPLICATION</span><h2>{item.outletName||'Unnamed outlet'}</h2><p>{item.planName||'Plan'} · {item.billingCycle||'Billing cycle'} · {item.paymentStatus||'Payment'}</p></div><button type="button" className="iconBtn" onClick={onClose}><Icon name="close"/></button></div><div className="verifyGrid"><section><h3>Business details</h3><div className="facts"><span>Type</span><b>{item.businessType||'—'}</b><span>Location</span><b>{[item.city,item.state,item.pincode].filter(Boolean).join(', ')||'—'}</b><span>Address</span><b>{[item.addressLine1,item.addressLine2].filter(Boolean).join(', ')||'—'}</b><span>Description</span><b>{item.description||'—'}</b></div></section><section><h3>Owner & identity</h3><div className="facts"><span>Owner</span><b>{item.ownerName||'—'}</b><span>Email</span><b>{item.ownerEmail||'—'}</b><span>Phone</span><b>{item.ownerPhone||'—'}</b><span>Aadhaar</span><b>XXXX XXXX {String(item.aadhaarNumber||'').slice(-4)||'—'}</b><span>Business PAN</span><b>{item.businessPan||'—'}</b><span>GST</span><b>{item.gstNumber||'—'}</b></div></section><section className="verifyDocs"><h3>Uploaded documents</h3><div className="docGrid">{docs.map(([label,url])=><button type="button" className="docRow" key={label} onClick={()=>onDocument(url)}><span className="docCheck"><Icon name="check" size={12}/></span><span><b>{label}</b><small>Open protected document</small></span><Icon name="arrow" size={14}/></button>)}{!docs.length&&<Empty text="No protected documents were uploaded."/>}</div></section><section className="decisionBox"><h3>Verification decision</h3><textarea value={notes} onChange={e=>setNotes(e.target.value)} placeholder="Add verification notes. Notes are required when rejecting."/><div className="verifyActions"><button type="button" className="secondaryBtn dangerBtn" disabled={loading||!notes.trim()} onClick={()=>onDecision(false)}>Reject</button><button type="button" className="primaryBtn" disabled={loading} onClick={()=>onDecision(true)}>Approve & activate <Icon name="check" size={13}/></button></div></section></div></div></div>;
}

createRoot(document.getElementById('root')).render(<App/>);
