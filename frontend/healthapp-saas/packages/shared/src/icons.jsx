import React from'react';
import{Home,LayoutDashboard,Repeat,CalendarDays,CreditCard,ClipboardList,MapPin,HeartPulse,Wallet,ChefHat,BookOpen,UtensilsCrossed,Wheat,Users,PackagePlus,Truck,Route,UserCog,Settings,Navigation,Circle}from'lucide-react';

const ICONS={home:Home,dashboard:LayoutDashboard,subscriptions:Repeat,calendar:CalendarDays,payment:CreditCard,orders:ClipboardList,addresses:MapPin,profile:HeartPulse,wallet:Wallet,kitchen:ChefHat,recipes:BookOpen,menu:UtensilsCrossed,'ingredient-usage':Wheat,customers:Users,packages:PackagePlus,deliveries:Truck,routes:Route,team:UserCog,settings:Settings,driver:Navigation};

/* One consistent outline icon set for sidebar navigation (replaces mixed emoji/glyphs). */
export function NavIcon({id,size=18}){
 const Cmp=ICONS[id]||Circle;
 return <Cmp size={size} strokeWidth={1.8} aria-hidden="true" className="hvIcon"/>;
}
