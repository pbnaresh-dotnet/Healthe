const CONFIGURED_API_BASE=String(import.meta.env.VITE_API_BASE_URL||'').trim();
const HOSTNAME=typeof globalThis!=='undefined'&&globalThis.location?.hostname?String(globalThis.location.hostname).toLowerCase():'';
const LOCAL_API_BASE=(HOSTNAME==='localhost'||HOSTNAME==='127.0.0.1'||HOSTNAME==='::1')?'http://localhost:50448/api':'';
const API_BASE=(CONFIGURED_API_BASE||LOCAL_API_BASE).replace(/\/$/,'');
export const API_URL=API_BASE;
export const CUSTOMER_URL=import.meta.env.VITE_CUSTOMER_URL||'http://localhost:5173';
export const TENANT_OUTLET_SLUG=String(import.meta.env.VITE_OUTLET_SLUG||'').trim().toLowerCase();
const DEMO_OUTLET_SLUG=(()=>{try{return new URLSearchParams(window.location.search).get('demo')?.trim().toLowerCase()||''}catch{return ''}})();

let runtimeTenantSlug=DEMO_OUTLET_SLUG||TENANT_OUTLET_SLUG;
let runtimeTenantHost=TENANT_OUTLET_SLUG?HOSTNAME:'';
export const getTenantOutletSlug=()=>{
  if(runtimeTenantSlug&&(!runtimeTenantHost||runtimeTenantHost===HOSTNAME))return runtimeTenantSlug;
  return '';
};
export async function resolveTenantFromHost(){
  if(TENANT_OUTLET_SLUG||!API_BASE||!HOSTNAME)return null;
  const correlationId=globalThis.crypto?.randomUUID?.()||`${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  let res;
  const startedAt=globalThis.performance?.now?.()??Date.now();
  try{
    res=await fetch(API_BASE+'/tenant/resolve?host='+encodeURIComponent(HOSTNAME),{headers:{'X-Correlation-Id':correlationId}});
  }catch(cause){
    const durationMs=Math.round((globalThis.performance?.now?.()??Date.now())-startedAt);
    if(import.meta.env.DEV)console.warn('[HealthApp API]',{method:'GET',route:'/tenant/resolve',status:0,durationMs,correlationId,error:'network-error'});
    const error=new Error('Unable to reach the service. Please try again.');error.correlationId=correlationId;error.cause=cause;throw error;
  }
  const responseCorrelationId=res.headers.get('X-Correlation-Id')||correlationId;
  const durationMs=Math.round((globalThis.performance?.now?.()??Date.now())-startedAt);
  if(import.meta.env.DEV&&(res.status>=500||durationMs>=1000))console.warn('[HealthApp API]',{method:'GET',route:'/tenant/resolve',status:res.status,durationMs,correlationId:responseCorrelationId});
  if(res.status===404)return null;
  if(!res.ok){const error=new Error('Unable to resolve the outlet for this hostname.');error.status=res.status;error.correlationId=responseCorrelationId;throw error;}
  const outlet=await res.json();
  const slug=String(outlet?.slug||'').trim().toLowerCase();
  if(!slug)return null;
  runtimeTenantSlug=slug;
  runtimeTenantHost=HOSTNAME;
  return outlet;
}
const readToken=()=>localStorage.getItem('ha_token');
export const currentUser=()=>{try{return JSON.parse(localStorage.getItem('ha_current_user')||'null')}catch{return null}};
const storeUser=u=>localStorage.setItem('ha_current_user',JSON.stringify(u));
export const money=n=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:2}).format(Number(n||0));
let cashfreeSdkPromise = null;
export const loadCashfree = (mode = String(import.meta.env.VITE_CASHFREE_MODE || 'sandbox').toLowerCase()) => {
  if (typeof window === 'undefined')
    return Promise.reject(new Error('Cashfree checkout is available only in a browser.'));
  if (window.Cashfree)
    return Promise.resolve(window.Cashfree({ mode }));
  if (cashfreeSdkPromise)
    return cashfreeSdkPromise.then(() => window.Cashfree({ mode }));

  cashfreeSdkPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector('script[data-cashfree-sdk]');
    if (existing) {
      existing.addEventListener('load', () => resolve(), { once: true });
      existing.addEventListener('error', () => reject(new Error('Unable to load Cashfree checkout.')), { once: true });
      return;
    }
    const script = document.createElement('script');
    script.src = 'https://sdk.cashfree.com/js/v3/cashfree.js';
    script.async = true;
    script.dataset.cashfreeSdk = 'true';
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('Unable to load Cashfree checkout.'));
    document.head.appendChild(script);
  });

  return cashfreeSdkPromise.then(() => {
    if (!window.Cashfree)
      throw new Error('Cashfree checkout SDK did not initialise.');
    return window.Cashfree({ mode });
  });
};

export async function openCashfreeCheckout(paymentSessionId, { mode = String(import.meta.env.VITE_CASHFREE_MODE || 'sandbox').toLowerCase(), redirectTarget = '_modal', onResult } = {}) {
  if (!paymentSessionId)
    throw new Error('Payment session is missing.');
  const cashfree = await loadCashfree(mode);
  return cashfree.checkout(
    { paymentSessionId, redirectTarget },
    result => {
      if (typeof onResult === 'function')
        onResult(result);
    });
}

export async function api(path,options={}){if(!API_BASE)throw new Error('API URL is not configured. Set VITE_API_BASE_URL in the Cloudflare build environment and redeploy.');const isFormData=typeof FormData!=='undefined'&&options.body instanceof FormData;
const correlationId=globalThis.crypto?.randomUUID?.()||`${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
const startedAt=globalThis.performance?.now?.()??Date.now();
const headers={...(isFormData?{}:{'Content-Type':'application/json'}),...(options.headers||{})};if(!headers['X-Correlation-Id'])headers['X-Correlation-Id']=correlationId;const tenantSlug=getTenantOutletSlug();if(tenantSlug&&!headers['X-Outlet-Slug'])headers['X-Outlet-Slug']=tenantSlug;const t=readToken();if(t)headers.Authorization=`Bearer ${t}`;let res;try{res=await fetch(`${API_BASE}${path}`,{...options,headers})}catch(cause){const durationMs=Math.round((globalThis.performance?.now?.()??Date.now())-startedAt);if(import.meta.env.DEV)console.warn('[HealthApp API]',{method:options.method||'GET',route:String(path).split('?')[0],status:0,durationMs,correlationId,error:'network-error'});const error=new Error('Unable to reach the service. Please try again.');error.correlationId=correlationId;error.cause=cause;throw error}const durationMs=Math.round((globalThis.performance?.now?.()??Date.now())-startedAt);const responseCorrelationId=res.headers.get('X-Correlation-Id')||correlationId;if(import.meta.env.DEV&&(res.status>=400||durationMs>=1000))console.warn('[HealthApp API]',{method:options.method||'GET',route:String(path).split('?')[0],status:res.status,durationMs,correlationId:responseCorrelationId});const raw=await res.text();let body=null;try{body=raw?JSON.parse(raw):null}catch{body=raw}if(res.status===401){const authEntryRequest=/^\/auth\/(login|register)(?:\?|$)/i.test(path);if(!authEntryRequest){localStorage.removeItem('ha_token');localStorage.removeItem('ha_current_user');if(typeof window!=='undefined')window.dispatchEvent(new CustomEvent('healthapp-auth-expired'));}const error=new Error(body?.message||body?.title||(authEntryRequest?'Invalid email or password.':'Your session has expired. Please sign in again.'));error.status=401;error.correlationId=responseCorrelationId;throw error}if(!res.ok){const error=new Error(body?.message||body?.title||body||`Request failed: ${res.status}`);error.status=res.status;error.correlationId=responseCorrelationId;throw error}return body;}
export const auth={async login(data){const tenantSlug=getTenantOutletSlug();const payload=tenantSlug?{...data,outletSlug:tenantSlug}:data;const x=await api('/auth/login',{method:'POST',body:JSON.stringify(payload)});localStorage.setItem('ha_token',x.accessToken);storeUser(x.user);return x;},async register(data){const tenantSlug=getTenantOutletSlug();const payload=tenantSlug?{...data,outletSlug:tenantSlug}:data;const x=await api('/auth/register',{method:'POST',body:JSON.stringify(payload)});localStorage.setItem('ha_token',x.accessToken);storeUser(x.user);return x;},logout(){localStorage.removeItem('ha_token');localStorage.removeItem('ha_current_user')},me(){return currentUser()}};
export const outlets={list:(city)=>api(`/marketplace/outlets${city?`?city=${encodeURIComponent(city)}`:''}`),get:slug=>api(`/marketplace/outlets/${encodeURIComponent(slug)}`),legal:slug=>api(`/marketplace/outlets/${encodeURIComponent(slug)}/legal`),plans:(outletId,city)=>api(`/marketplace/outlets/${outletId}/meal-plans${city?`?city=${encodeURIComponent(city)}`:''}`),availability:(lat,lng,city)=>api(`/marketplace/availability?latitude=${lat}&longitude=${lng}${city?`&city=${encodeURIComponent(city)}`:''}`)};
export const locations={cities:()=>api('/marketplace/cities'),areas:city=>api(`/marketplace/city-areas${city?`?city=${encodeURIComponent(city)}`:''}`),reverseGeocode:(latitude,longitude)=>api(`/marketplace/reverse-geocode?latitude=${latitude}&longitude=${longitude}`)};
export const catalog={ingredients:()=>api('/catalog/ingredients'),allergens:()=>api('/catalog/allergens')};
export async function prepareImageForUpload(file,kind='general'){
 if(typeof File==='undefined'||!(file instanceof File)||!String(file.type||'').toLowerCase().startsWith('image/')) return file;
 if(typeof document==='undefined'||typeof createImageBitmap==='undefined') return file;
 const maxByKind={recipe:2200,hero:2400,logo:512,favicon:256,avatar:1024,general:2200};
 const max=maxByKind[kind]||maxByKind.general;
 try{
   const bitmap=await createImageBitmap(file);
   const largest=Math.max(bitmap.width,bitmap.height);
   const scale=Math.min(1,max/largest);
   const width=Math.max(1,Math.round(bitmap.width*scale));
   const height=Math.max(1,Math.round(bitmap.height*scale));
   if(scale===1&&file.size<=750*1024&&file.type==='image/webp'){bitmap.close();return file;}
   const canvas=document.createElement('canvas');
   canvas.width=width;canvas.height=height;
   const ctx=canvas.getContext('2d');
   if(!ctx){bitmap.close();return file;}
   ctx.drawImage(bitmap,0,0,width,height);
   bitmap.close();
   const blob=await new Promise(resolve=>canvas.toBlob(resolve,'image/webp',0.82));
   if(!blob)return file;
   return new File([blob],((file.name||'image').replace(/\.[^.]+$/,'')||'image')+'.webp',{type:'image/webp',lastModified:Date.now()});
 }catch{return file;}
}
export const files={upload:async(folder,file)=>{
 const kind=folder==='avatars'?'avatar':folder==='recipes'?'recipe':folder==='outlets'?'hero':'general';
 const prepared=await prepareImageForUpload(file,kind);
 const form=new FormData();
 form.append('file',prepared);
 return api(`/files/${encodeURIComponent(folder)}`,{method:'POST',body:form});
}};
export const recipes={list:(outletId,category)=>api(`/marketplace/outlets/${outletId}/recipes${category?`?category=${encodeURIComponent(category)}`:''}`),forOutlet:category=>api(`/outlets/me/recipes${category?`?category=${encodeURIComponent(category)}`:''}`),create:data=>api('/outlets/me/recipes',{method:'POST',body:JSON.stringify(data)}),update:(id,data)=>api(`/outlets/me/recipes/${id}`,{method:'PUT',body:JSON.stringify(data)}),remove:id=>api(`/outlets/me/recipes/${id}`,{method:'DELETE'})};
export const menu={outlet:id=>api(`/marketplace/outlets/${id}/menu`),mine:()=>api('/outlets/me/menu'),save:items=>api('/outlets/me/menu',{method:'PUT',body:JSON.stringify({items})})};
export const customer={profile:()=>api('/customer/profile/preferences'),saveProfile:data=>api('/customer/profile/preferences',{method:'PUT',body:JSON.stringify(data)}),updateMarketingPreference:data=>api('/customer/marketing-preference',{method:'PATCH',body:JSON.stringify(data)}),legalStatus:()=>api('/customer/legal-status'),acceptLegal:data=>api('/customer/legal-acceptance',{method:'POST',body:JSON.stringify(data)}),likedMeals:()=>api('/customer/liked-meals'),likeMeal:recipeId=>api(`/customer/liked-meals/${recipeId}`,{method:'POST'}),unlikeMeal:recipeId=>api(`/customer/liked-meals/${recipeId}`,{method:'DELETE'}),addresses:()=>api('/customer/addresses'),createAddress:data=>api('/customer/addresses',{method:'POST',body:JSON.stringify(data)}),updateAddress:(id,data)=>api(`/customer/addresses/${id}`,{method:'PUT',body:JSON.stringify(data)}),deleteAddress:id=>api(`/customer/addresses/${id}`,{method:'DELETE'}),deliveryQuotes:outletId=>api(`/customer/outlets/${outletId}/delivery-quotes`),subscriptions:()=>api('/customer/subscriptions'),dashboard:()=>api('/customer/dashboard'),orders:()=>api('/customer/orders'),quote:data=>api('/customer/subscriptions/quote',{method:'POST',body:JSON.stringify(data)}),subscribe:data=>api('/customer/subscriptions',{method:'POST',body:JSON.stringify(data)}),recipes:(id,category)=>api(`/customer/subscriptions/${id}/recipes${category?`?category=${encodeURIComponent(category)}`:''}`),menu:id=>api(`/customer/subscriptions/${id}/menu`),mealSelections:(id,weekStart)=>api(`/customer/subscriptions/${id}/meal-selections${weekStart?`?weekStart=${encodeURIComponent(weekStart)}`:''}`),saveSelections:(id,data)=>api(`/customer/subscriptions/${id}/meal-selections`,{method:'PUT',body:JSON.stringify(data)}),skipMeal:(sid,mid,reason)=>api(`/customer/subscriptions/${sid}/meal-selections/${mid}/skip`,{method:'POST',body:JSON.stringify({reason})}),skipDay:(sid,date,reason)=>api(`/customer/subscriptions/${sid}/days/${encodeURIComponent(date)}/skip`,{method:'POST',body:JSON.stringify({reason})}),rescheduleMeal:(sid,mid,data)=>api(`/customer/subscriptions/${sid}/meal-selections/${mid}/reschedule`,{method:'POST',body:JSON.stringify(data)}),credit:()=>api('/customer/credit'),creditTransactions:()=>api('/customer/credit/transactions'),pay:(subscriptionId,idempotencyKey,provider='Cashfree')=>api('/customer/payments',{method:'POST',body:JSON.stringify({subscriptionId,idempotencyKey,provider})}),getPayment:id=>api(`/customer/payments/${id}`),acceptPackage:(subscriptionId,data)=>api(`/customer/packages/${subscriptionId}/accept`,{method:'POST',body:JSON.stringify(data)})};
export const outletDemo={request:data=>api('/outlet-demo/request',{method:'POST',body:JSON.stringify(data)})};
export const outletOnboarding={plans:()=>api('/outlet-onboarding/plans'),pay:data=>api('/outlet-onboarding/payment',{method:'POST',body:JSON.stringify(data)}),me:()=>api('/outlet-onboarding/me'),saveCurrentDetails:data=>api('/outlet-onboarding/me/details',{method:'PUT',body:JSON.stringify(data)}),uploadCurrentDocument:(type,file)=>{const form=new FormData();form.append('documentType',type);form.append('file',file);return api('/outlet-onboarding/me/documents',{method:'POST',body:form})},submitCurrent:()=>api('/outlet-onboarding/me/submit',{method:'POST'}),get:(id,key)=>api('/outlet-onboarding/'+id,{headers:{'X-Onboarding-Key':key}}),saveDetails:(id,key,data)=>api('/outlet-onboarding/'+id+'/details',{method:'PUT',headers:{'X-Onboarding-Key':key},body:JSON.stringify(data)}),uploadDocument:(id,key,type,file)=>{const form=new FormData();form.append('documentType',type);form.append('file',file);return api('/outlet-onboarding/'+id+'/documents',{method:'POST',headers:{'X-Onboarding-Key':key},body:form})},submit:(id,key)=>api('/outlet-onboarding/'+id+'/submit',{method:'POST',headers:{'X-Onboarding-Key':key}})};
export const outletAdmin={settings:()=>api('/outlets/me/settings'),managerSettings:()=>api('/outlets/me/settings/manager'),legal:()=>api('/outlets/me/settings/legal'),updateLegal:data=>api('/outlets/me/settings/legal',{method:'PUT',body:JSON.stringify(data)}),domains:()=>api('/outlets/me/settings/domains'),requestDomain:(hostname,isPrimary=false)=>api('/outlets/me/settings/domains',{method:'POST',body:JSON.stringify({hostname,isPrimary})}),verifyDomain:(id,activateIfReady=false)=>api(`/outlets/me/settings/domains/${id}/verify`,{method:'POST',body:JSON.stringify({activateIfReady})}),updateBranding:data=>api('/outlets/me/settings/branding',{method:'PUT',body:JSON.stringify(data)}),uploadBrandingAsset:async(assetType,file)=>{const kind=assetType==='logo'?'logo':assetType==='favicon'?'favicon':'hero';const prepared=await prepareImageForUpload(file,kind);const form=new FormData();form.append('assetType',assetType);form.append('file',prepared);return api('/outlets/me/settings/branding/assets',{method:'POST',body:form});},readiness:()=>api('/outlets/me/settings/readiness'),updateDeliveryDays:(deliveryDays,deliveryCoverageMode='Radius',serviceRadiusKm=null,closures=undefined)=>api('/outlets/me/settings/delivery-days',{method:'PUT',body:JSON.stringify({deliveryDays,deliveryCoverageMode,serviceRadiusKm,...(closures===undefined?{}:{closures})})}),updatePackageSettings:data=>api('/outlets/me/settings/package-settings',{method:'PUT',body:JSON.stringify(data)}),updateLateSkipFee:lateSkipFee=>api('/outlets/me/settings/late-skip-fee',{method:'PUT',body:JSON.stringify({lateSkipFee:Number(lateSkipFee)})}),goLive:()=>api('/outlets/me/settings/go-live',{method:'POST'}),mealPlans:()=>api('/outlets/me/meal-plans'),createMealPlan:data=>api('/outlets/me/meal-plans',{method:'POST',body:JSON.stringify(data)}),outlet:()=>api('/outlets/me'),dashboard:()=>api('/outlets/me/dashboard'),subscriptionDetail:id=>api(`/outlets/me/subscriptions/${id}`),kitchen:date=>api(`/outlets/me/kitchen${date?`?date=${encodeURIComponent(date)}`:''}`),markFoodReady:deliveryId=>api(`/outlets/me/delivery-labels/deliveries/${deliveryId}/food-ready`,{method:'POST'}),markMealFoodReady:selectionId=>api(`/outlets/me/delivery-labels/selections/${selectionId}/food-ready`,{method:'POST'}),ingredientConsumption:date=>api(`/outlets/me/reports/ingredient-consumption${date?`?date=${encodeURIComponent(date)}`:''}`),drivers:()=>api('/outlets/me/delivery-routes/drivers'),createDriver:data=>api('/outlets/me/delivery-routes/drivers',{method:'POST',body:JSON.stringify(data)}),deliveryRoutes:(date,mealSlot=2)=>api(`/outlets/me/delivery-routes?date=${encodeURIComponent(date||'')}&mealSlot=${mealSlot}`),planDeliveryRoutes:data=>api('/outlets/me/delivery-routes/plan',{method:'POST',body:JSON.stringify(data)}),dispatchRoute:id=>api(`/outlets/me/delivery-routes/${id}/dispatch`,{method:'POST'}),myDriverRoute:(date,mealSlot=2)=>api(`/outlets/me/delivery-routes/my?date=${encodeURIComponent(date||'')}&mealSlot=${mealSlot}`),startDriverRoute:id=>api(`/outlets/me/delivery-routes/${id}/start`,{method:'POST'}),pickUpDriverStop:id=>api(`/outlets/me/delivery-routes/stops/${id}/pickup`,{method:'POST'}),pickUpDriverDelivery:id=>api(`/outlets/me/delivery-routes/deliveries/${id}/pickup`,{method:'POST'}),completeDriverStop:id=>api(`/outlets/me/delivery-routes/stops/${id}/complete`,{method:'POST'}),billing:()=>api('/outlets/me/billing'),subscriptionPlans:()=>api('/outlets/me/subscription/plans'),changeSubscription:(saasPlanId,billingCycle)=>api('/outlets/me/subscription',{method:'PUT',body:JSON.stringify({saasPlanId,billingCycle})}),customers:(query=null)=>api('/outlets/me/customers'+(query?'?'+new URLSearchParams(query).toString():'')),subscriptions:(query=null)=>api('/outlets/me/subscriptions'+(query?'?'+new URLSearchParams(query).toString():'')),orders:(query=null)=>api('/outlets/me/orders'+(query?'?'+new URLSearchParams(query).toString():'')),deliveries:(query=null)=>api('/outlets/me/deliveries'+(query?'?'+new URLSearchParams(query).toString():'')),menu:()=>api('/outlets/me/menu'),saveMenu:items=>api('/outlets/me/menu',{method:'PUT',body:JSON.stringify({items})}),recipes:(query=null)=>query?api('/outlets/me/recipes?'+new URLSearchParams(query).toString()):recipes.forOutlet(),createRecipe:data=>recipes.create(data),updateRecipe:(id,data)=>recipes.update(id,data),deleteRecipe:id=>recipes.remove(id),uploadRecipeImage:async file=>{const prepared=await prepareImageForUpload(file,'recipe');const form=new FormData();form.append('file',prepared);return api('/outlets/me/recipes/image',{method:'POST',body:form});},pricingRules:()=>api('/outlets/me/delivery-pricing'),taxSettings:()=>api('/outlets/me/tax-settings'),updateTaxSettings:data=>api('/outlets/me/tax-settings',{method:'PUT',body:JSON.stringify(data)}),addPricingRule:data=>api('/outlets/me/delivery-pricing',{method:'POST',body:JSON.stringify(data)}),deletePricingRule:id=>api(`/outlets/me/delivery-pricing/${id}`,{method:'DELETE'}),availableDeliveryAreas:city=>api(`/outlets/me/delivery-areas/available${city?`?city=${encodeURIComponent(city)}`:''}`),selectedDeliveryAreas:()=>api('/outlets/me/delivery-areas'),saveDeliveryAreas:cityAreaIds=>api('/outlets/me/delivery-areas',{method:'PUT',body:JSON.stringify({cityAreaIds})}),discountTiers:()=>api('/outlets/me/discount-tiers'),addDiscountTier:data=>api('/outlets/me/discount-tiers',{method:'POST',body:JSON.stringify(data)}),updateDiscountTier:(id,data)=>api(`/outlets/me/discount-tiers/${id}`,{method:'PUT',body:JSON.stringify(data)}),deleteDiscountTier:id=>api(`/outlets/me/discount-tiers/${id}`,{method:'DELETE'}),createCustomer:data=>api('/outlets/me/customers',{method:'POST',body:JSON.stringify(data)}),customerProfile:customerId=>api(`/outlets/me/customers/${customerId}/profile`),updateCustomerProfile:(customerId,data)=>api(`/outlets/me/customers/${customerId}/profile`,{method:'PUT',body:JSON.stringify(data)}),customerAddresses:customerId=>api(`/outlets/me/customers/${customerId}/addresses`),createCustomerAddress:(customerId,data)=>api(`/outlets/me/customers/${customerId}/addresses`,{method:'POST',body:JSON.stringify(data)}),packageQuote:data=>api('/outlets/me/packages/quote',{method:'POST',body:JSON.stringify(data)}),createPackage:data=>api('/outlets/me/packages',{method:'POST',body:JSON.stringify(data)}),markPackagePaid:(id,data)=>api(`/outlets/me/packages/${id}/mark-paid`,{method:'POST',body:JSON.stringify(data)}),confirmCustomerPackage:(id,data={})=>api(`/outlets/me/packages/${id}/confirm`,{method:'POST',body:JSON.stringify(data)})};
export const outletStaff={list:()=>api('/outlets/me/staff'),create:data=>api('/outlets/me/staff',{method:'POST',body:JSON.stringify(data)}),update:(id,data)=>api(`/outlets/me/staff/${id}`,{method:'PUT',body:JSON.stringify(data)})};
export const admin={groups:()=>api('/admin/groups'),createGroup:data=>api('/admin/groups',{method:'POST',body:JSON.stringify(data)}),updateGroup:(id,data)=>api(`/admin/groups/${id}`,{method:'PUT',body:JSON.stringify(data)}),assignOutletGroup:(outletId,groupId)=>api(`/admin/outlets/${outletId}/group`,{method:'PUT',body:JSON.stringify({outletGroupId:groupId||null})}),outlet360:id=>api(`/admin/outlets/${id}/360`),domains:()=>api('/admin/domains'),setDomainStatus:(id,status)=>api(`/admin/domains/${id}/status`,{method:'PUT',body:JSON.stringify({status})}),outletOnboardingPending:()=>api('/admin/outlet-onboarding'),outletOnboardingDetail:id=>api('/admin/outlet-onboarding/'+id),decideOutletOnboarding:(id,data)=>api('/admin/outlet-onboarding/'+id+'/decision',{method:'POST',body:JSON.stringify(data)}),outlets:()=>api('/admin/outlets'),outletsPage:(params={})=>{const q=new URLSearchParams();Object.entries(params||{}).forEach(([k,v])=>{if(v!==undefined&&v!==null&&v!=='')q.set(k,String(v))});return api(`/admin/outlets/page${q.toString()?`?${q.toString()}`:''}`)},saasPlans:()=>api('/admin/saas-plans'),createOutlet:data=>api('/admin/outlets',{method:'POST',body:JSON.stringify(data)}),markOutletPaid:(outletId,data)=>api(`/admin/outlets/${outletId}/mark-paid`,{method:'POST',body:JSON.stringify(data)}),reactivationOptions:outletId=>api(`/admin/outlets/${outletId}/reactivation-options`),reactivateOutlet:(outletId,data)=>api(`/admin/outlets/${outletId}/reactivate`,{method:'POST',body:JSON.stringify(data)}),users:()=>api('/admin/users'),dashboard:()=>api('/admin/dashboard'),revenue:()=>api('/admin/reports/revenue'),saasBillingSettings:()=>api(`/saas-billing/settings`),saasInvoices:(params={})=>{const q=new URLSearchParams();Object.entries(params||{}).forEach(([k,v])=>{if(v!==undefined&&v!==null&&v!=='')q.set(k,String(v))});return api(`/saas-billing/invoices${q.toString()?`?${q.toString()}`:''}`)},generateSaaSInvoices:data=>api('/saas-billing/generate',{method:'POST',body:JSON.stringify(data)}),saasInvoice:(id,params={})=>{const q=new URLSearchParams();Object.entries(params||{}).forEach(([k,v])=>{if(v!==undefined&&v!==null&&v!=='')q.set(k,String(v))});return api(`/saas-billing/invoices/${id}${q.toString()?`?${q.toString()}`:''}`)},recordSaaSPayment:(id,data)=>api(`/saas-billing/invoices/${id}/payments`,{method:'POST',body:JSON.stringify(data)}),generateSaaSPaymentLink:(id,data={})=>api(`/saas-billing/invoices/${id}/payment-link`,{method:'POST',body:JSON.stringify(data)}),finance:(params={})=>{const q=new URLSearchParams();Object.entries(params||{}).forEach(([k,v])=>{if(v!==undefined&&v!==null&&v!=='')q.set(k,String(v))});return api(`/admin/reports/finance${q.toString()?`?${q.toString()}`:''}`)},financePolicy:(code='FINANCE-CALCULATION-POLICY',asOfUtc='')=>api(`/admin/finance-policy?code=${encodeURIComponent(code)}${asOfUtc?`&asOfUtc=${encodeURIComponent(asOfUtc)}`:''}`),financePolicyHistory:(code='FINANCE-CALCULATION-POLICY')=>api(`/admin/finance-policy/history?code=${encodeURIComponent(code)}`),cities:()=>api('/admin/cities'),createCity:data=>api('/admin/cities',{method:'POST',body:JSON.stringify(data)}),setCityEnabled:(id,enabled)=>api(`/admin/cities/${id}/enabled?enabled=${enabled}`,{method:'PUT'}),cityAreas:city=>api(`/admin/city-areas${city?`?city=${encodeURIComponent(city)}`:''}`),createCityArea:data=>api('/admin/city-areas',{method:'POST',body:JSON.stringify(data)}),areaManagers:()=>api('/admin/area-managers'),createAreaManager:data=>api('/admin/area-managers',{method:'POST',body:JSON.stringify(data)}),assignAreaManagerOutlets:(id,outletIds)=>api(`/admin/area-managers/${id}/outlets`,{method:'PUT',body:JSON.stringify({outletIds})}),setAreaManagerStatus:(id,isActive)=>api(`/admin/area-managers/${id}/status`,{method:'PATCH',body:JSON.stringify({isActive})}),areaManagerDashboard:()=>api('/area-manager/dashboard'),myManagedOutlets:()=>api('/area-manager/outlets'),diagnosticsSettings:()=>api('/admin/diagnostics/settings'),updateDiagnosticsSettings:data=>api('/admin/diagnostics/settings',{method:'PUT',body:JSON.stringify(data)}),errors:(params={})=>{const q=new URLSearchParams();Object.entries(params||{}).forEach(([k,v])=>{if(v!==undefined&&v!==null&&v!=='')q.set(k,String(v))});return api(`/admin/errors${q.toString()?`?${q.toString()}`:''}`)},error:(id)=>api(`/admin/errors/${id}`),resolveError:(id,notes='')=>api(`/admin/errors/${id}/resolve`,{method:'PUT',body:JSON.stringify({resolutionNotes:notes})})};
export function distanceKm(lat1,lon1,lat2,lon2){const R=6371,dLat=(lat2-lat1)*Math.PI/180,dLon=(lon2-lon1)*Math.PI/180,a=Math.sin(dLat/2)**2+Math.cos(lat1*Math.PI/180)*Math.cos(lat2*Math.PI/180)*Math.sin(dLon/2)**2;return R*2*Math.atan2(Math.sqrt(a),Math.sqrt(1-a));}

export {default as PackageBuilder} from './PackageBuilder.jsx';

export {AppFeedbackProvider,useFeedback,AppModal,AppAlert} from './Feedback.jsx';
