using MelonLoader;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VRCModNetwork;

// Les informations générales relatives à un assembly dépendent de
// l'ensemble d'attributs suivant. Changez les valeurs de ces attributs pour modifier les informations
// associées à un assembly.
[assembly: AssemblyTitle("VRCModNetworkLib")]
[assembly: AssemblyDescription("VRCModNetwork library-mod for VRChat over MelonLoader")]
[assembly: AssemblyCompany("VRChat Modding Group")]
[assembly: AssemblyProduct("VRCModNetworkLib")]
[assembly: AssemblyCopyright("Copyright © Slaynash 2018-2020")]

// L'affectation de la valeur false à ComVisible rend les types invisibles dans cet assembly
// aux composants COM. Si vous devez accéder à un type dans cet assembly à partir de
// COM, affectez la valeur true à l'attribut ComVisible sur ce type.
[assembly: ComVisible(false)]

// Le GUID suivant est pour l'ID de la typelib si ce projet est exposé à COM
[assembly: Guid("8c20badc-26b2-4f7b-a141-b701c8bd91de")]

// Les informations de version pour un assembly se composent des quatre valeurs suivantes :
//
//      Version principale
//      Version secondaire
//      Numéro de build
//      Révision
//
// Vous pouvez spécifier toutes les valeurs ou indiquer les numéros de build et de révision par défaut
// en utilisant '*', comme indiqué ci-dessous :
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

[assembly: MelonModInfo(typeof(VRCModNetworkLib), "VRCModNetworkLib", "0.1.0", "Slaynash")]
[assembly: MelonModGame("VRChat", "VRChat")]