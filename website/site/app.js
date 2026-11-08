(() => {
  'use strict';
  const defaultRepository = 'https://github.com/Sheekovic/Revora-OpenSource';
  const config = window.REVORA_CONFIG || {};
  const english = Object.fromEntries([...document.querySelectorAll('[data-i18n]')].map(element => [element.dataset.i18n, element.textContent]));
  english.heroNote = 'Your realm.\nYour rules.';
  english.footerTagline = 'An independent vision.\nA world worth building.';
  const arabic = {
    skip: 'انتقل إلى المحتوى', preview: 'معاينة مفتوحة المصدر · خدمات اللعبة غير متصلة', viewSource: 'تصفح السورس',
    world: 'العالم', features: 'المميزات', news: 'الأخبار', downloads: 'التنزيلات', account: 'الحساب',
    heroEyebrow: 'عالم مألوف. رؤية مستقلة.', heroLine1: 'الأساطير لا تُولد.', heroLine2: 'بل تُصنع.',
    heroLead: 'استعد شغف كونكر. ابنِ عالمًا من المنافسات القوية، والحلفاء غير المتوقعين، والحكايات التي تستحق أن تُروى.',
    getRevora: 'احصل على Revora', discover: 'اكتشف العالم', heroCaption: 'باتش 6711 · DirectX 9 · مجاني ومفتوح المصدر',
    scroll: 'عالم ينتظرك', heroNote: 'عالمك.\nقواعدك.', stripTitle: 'مشروع ريفورا', serverTarget: 'إصدار السيرفر', rendering: 'الرؤية الكلاسيكية', openLicense: 'ترخيص مفتوح المصدر', starGitHub: 'ضع نجمة على GitHub',
    worldEyebrow: 'خلف بوابات المدينة', worldTitle1: 'عالم يستحق', worldTitle2: 'أن تعود إليه.',
    worldDescription: 'روح المغامرة التي تعرفها، ومساحة لتصنع رؤيتك الخاصة. اكتشف الأنظمة والأفكار التي تقف وراء ريفورا.',
    tabExplore: '01 / استكشف', tabCompanions: '02 / اعثر على حلفائك', tabRivalries: '03 / ارتقوا معًا', artwork: 'تصور فني لريفورا', exploreFeatures: 'استكشف دليل المميزات',
    featuresEyebrow: 'بُني بعناية', featuresTitle: 'أكثر من مجرد عالم آخر.',
    featuresDescription: 'سيرفر ولانشر وموقع في مشروع واحد، ليشارك المجتمع في بناء الفصل القادم.',
    feature1Title: 'عالم ينبض بالحياة', feature1Body: 'حراس للمدن، ومغامرون جوّالون، ورفاق، ومجموعة متنامية من المهام وأنظمة التقدم.',
    feature2Title: 'لانشر مدروس', feature2Body: 'تحديثات قابلة للاسترجاع، وفحوص للملفات، وأدوات للإصلاح، وأساس لربط نسخة العميل المعتمدة الخاصة بك.',
    feature3Title: 'ابنِ رؤيتك الخاصة', feature3Body: 'سورس قابل للقراءة وحدود موثقة وموقع بـ HTML وCSS وJavaScript. بدون خطوات بناء للواجهة.',
    featureNote: 'المميزات تصف مشروع السورس. التشغيل القابل للعب يحتاج أيضًا إلى المتطلبات الموثقة واختبار توافق العميل.',
    newsEyebrow: 'من ورشة التطوير', newsTitle: 'آخر الحكايات.', allUpdates: 'كل تحديثات التطوير', projectNews: 'أخبار المشروع', development: 'التطوير', websiteLabel: 'الموقع', newsDate: '08 نوفمبر 2026',
    news1Title: 'فصل جديد. متاح للجميع.', news1Body: 'مئات الساعات من العمل، نشاركها مع المجتمع مجانًا.',
    news2Title: 'مشروع واحد. كل شيء قريب.', news2Body: 'سيرفر 6711 ولانشر PlayRevora والموقع في مكان واحد.',
    news3Title: 'طريقة أسهل لمشاركة عالمك.', news3Body: 'ملفات ويب بسيطة. لغتان. جاهز لـ GitHub Pages.', readStory: 'اقرأ الحكاية',
    downloadEyebrow: 'الفصل القادم بين يديك', downloadTitle1: 'لا تنضم إلى عالم فحسب.', downloadTitle2: 'اصنع عالمك الخاص.',
    downloadDescription: 'ابدأ بالسورس، واتبع دليل الإعداد، واصنع ريفورا بطريقتك. المشروع مجاني، والاختيارات لك.',
    downloadSource: 'نزّل السورس', setupGuide: 'اقرأ دليل الإعداد', serverCode: 'سيرفر 6711', launcherCode: 'لانشر Windows', websiteCode: 'موقع HTML / CSS / JS',
    downloadNote: 'التنزيل للسورس فقط. عميل اللعبة المملوك للناشر وبيانات التشغيل غير مضمنين.',
    faqEyebrow: 'قبل رحلتك', faqTitle: 'أشياء من المفيد معرفتها.', faqIntro: 'بداية واضحة، سواء كنت تستكشف المشروع أو تبني عالمك الخاص.',
    faq1: 'هل ريفورا مجاني فعلًا؟', faq1Answer: 'نعم. سورس ريفورا الأصلي متاح بترخيص MIT، وتحتفظ المكتبات الخارجية بتراخيصها. النجوم والمتابعة والتبرعات الاختيارية تساعد على دعم العمل.',
    faq2: 'هل يحتاج الموقع إلى ASP.NET؟', faq2Answer: 'لا. الموقع مبني بـ HTML وCSS وJavaScript. افتح index.html، أو شغّل خادم ملفات بسيطًا، أو انشر مجلد الموقع على GitHub Pages. خدمات الحسابات تُربط بباك إند منفصل.',
    faq3: 'هل يمكنني اللعب من هذه المعاينة؟', faq3Answer: 'هذه معاينة عامة للموقع. لا يوجد سيرفر لعبة أو خدمة حسابات متصلة بها. يحتاج المشغل إلى نسخة تشغيل 6711 معتمدة وبيانات اللعبة وعميل متوافق.',
    faq4: 'من أين أبدأ في تشغيل السيرفر؟', faq4Answer: 'نزّل الريبو واقرأ QUICKSTART.md. ملف الإعداد يفحص المتطلبات ويجهز الخدمات المحلية. يجب توفير المكتبات القديمة وبيانات التشغيل الخاصة بصورة قانونية.',
    supportTitle: 'صُنع بالوقت. وأُتيح للجميع.', supportBody: 'مئات الساعات. مئات الدولارات. ومشروع متاح مجانًا. إن كان ريفورا يعني لك شيئًا، اترك نجمة أو ساهم في دعم فصله القادم.', supportButton: 'ادعم صاحب المشروع',
    footerTagline: 'رؤية مستقلة.\nوعالم يستحق البناء.', followCreator: 'تابع Sheekovic ↗', privacy: 'الخصوصية', terms: 'شروط المشروع', byCreator: 'من تطوير Sheekovic.', notAffiliated: 'مشروع مستقل وغير تابع لناشر اللعبة الأصلية.', backTop: 'العودة للأعلى ↑', viewGitHub: 'تصفح على GitHub'
  };
  const worlds = {
    explore: { image: 'dragon-realm.png', number: 'I', en: ['THE JOURNEY', 'Take the road less traveled.', 'From familiar cities to ambitious adventures, Revora brings its own approach to a world shaped by exploration and progression.'], ar: ['الرحلة', 'اسلك طريقك الخاص.', 'من المدن المألوفة إلى المغامرات الطموحة، تقدم ريفورا رؤيتها لعالم تصنعه الرحلات والتقدم.'] },
    companions: { image: 'celestial-festival.png', number: 'II', en: ['UNEXPECTED ALLIES', 'Adventure finds good company.', 'Living-world adventurers, city guards, and companion orders give the source its own character. Explore the documented follow, assist, protect, and retreat systems.'], ar: ['حلفاء غير متوقعين', 'المغامرة تحلو بالرفاق.', 'مغامرون في العالم وحراس للمدن وأوامر للرفاق تمنح السورس طابعه الخاص. استكشف أنظمة المتابعة والمساندة والحماية والانسحاب الموثقة.'] },
    rivalries: { image: 'shadowfall-trials.png', number: 'III', en: ['YOUR NEXT CHAPTER', 'Leave your mark on the realm.', 'Quests, progression, tournaments, and social systems form a foundation to build on. Read the feature ledger for what is implemented and what still needs a real-client test.'], ar: ['فصلك القادم', 'اترك بصمتك في العالم.', 'المهام والتقدم والبطولات والأنظمة الاجتماعية أساس لتبني عليه. اقرأ سجل المميزات لمعرفة ما نُفذ وما يزال يحتاج إلى اختبار بعميل حقيقي.'] }
  };
  const stories = {
    open: {
      en: ['A new chapter. Open to everyone.', 'Revora represents hundreds of hours of modification, development, and testing, along with hundreds of dollars spent by Sheekovic. The current project is now shared freely under the MIT license for Revora-authored material.', 'If the work helps you, give the repository a star, follow the creator, or make a voluntary PayPal donation. Third-party components retain their own license terms.'],
      ar: ['فصل جديد. متاح للجميع.', 'وراء ريفورا مئات الساعات من التعديل والتطوير والاختبار، ومئات الدولارات التي أنفقها Sheekovic. المشروع الحالي متاح مجانًا بترخيص MIT لكود ريفورا الأصلي.', 'إذا استفدت من هذا العمل، اترك نجمة على الريبو، أو تابع صاحب المشروع، أو ادعمه بتبرع اختياري عبر PayPal. تحتفظ المكونات الخارجية بتراخيصها.'], path: '/blob/main/README.md'
    },
    together: {
      en: ['One project. Everything in reach.', 'The monorepo brings together Revora6711-3D, PlayRevora Launcher, and the website. Source commits and migration details are documented, and the original repositories remain intact.', 'The game server targets 6711 DX9. Imported launcher profiles include 7630 work, so a reviewed matching profile and a real-client test are still needed before claiming a complete playable installation.'],
      ar: ['مشروع واحد. كل شيء قريب.', 'يجمع الريبو Revora6711-3D ولانشر PlayRevora والموقع. وُثقت إصدارات المصادر وتفاصيل التجميع، مع الحفاظ على الريبوهات الأصلية.', 'يستهدف السيرفر باتش 6711 DX9. بعض إعدادات اللانشر المستوردة تخص 7630؛ لذلك يلزم إعداد متوافق معتمد واختبار بعميل حقيقي قبل اعتبار التشغيل متكاملًا.'], path: '/blob/main/docs/MIGRATION.md'
    },
    web: {
      en: ['A simpler way to share your realm.', 'The public website is now buildless HTML, CSS, and JavaScript, with English and Arabic layouts. It can run directly from its files or on a static host such as GitHub Pages.', 'Account services, game processes, and PostgreSQL belong on an operator-controlled backend. No database password or private key belongs in the browser.'],
      ar: ['طريقة أسهل لمشاركة عالمك.', 'الموقع العام أصبح ملفات HTML وCSS وJavaScript، بالعربية والإنجليزية. يمكن تشغيله مباشرة أو نشره على استضافة ثابتة مثل GitHub Pages.', 'خدمات الحسابات واللعبة وPostgreSQL تعمل على باك إند يتحكم فيه المشغل. كلمات مرور الداتابيز والمفاتيح الخاصة لا توضع في المتصفح.'], path: '/blob/main/docs/WEBSITE.md'
    },
    account: {
      en: ['Account services are not connected.', 'This public preview does not create accounts or collect passwords. If you are exploring Revora, start with the source and setup guide.', 'Operators can configure their own HTTPS account website after its backend and 6711 integration have been validated.'],
      ar: ['خدمات الحسابات غير متصلة.', 'هذه المعاينة لا تنشئ حسابات ولا تجمع كلمات مرور. إن كنت تستكشف ريفورا، ابدأ بالسورس ودليل الإعداد.', 'يمكن للمشغل ربط موقع حسابات HTTPS خاص به بعد اختبار الباك إند وتكامل 6711.'], path: '/blob/main/docs/CONNECT_COMPONENTS.md'
    },
    privacy: {
      en: ['Privacy, with less to collect.', 'No analytics or account collection is built into this static website. Your chosen language may be saved in your browser. No credentials are stored by the site.', 'GitHub Pages, GitHub links, and PayPal are external services with their own privacy practices. A self-hosted operator is responsible for its own backend and account policy.'],
      ar: ['خصوصية تبدأ بتقليل البيانات.', 'لا يتضمن هذا الموقع الثابت تحليلات أو جمعًا للحسابات. قد يُحفظ اختيار اللغة في متصفحك. لا يحفظ الموقع بيانات دخول.', 'GitHub Pages وGitHub وPayPal خدمات خارجية لها سياسات خصوصية خاصة بها. يتحمل مشغل النسخة الخاصة مسؤولية الباك إند وسياسة الحسابات.'], path: '/blob/main/SECURITY.md'
    },
    terms: {
      en: ['A source project, shared openly.', 'Revora-authored material is available under the MIT license, without warranty. Third-party notices and license conditions remain in force.', 'This preview does not provide a playable game service or paid game items. Proprietary clients, third-party assets, and missing external assemblies are not licensed or distributed by the source download.'],
      ar: ['مشروع سورس متاح للجميع.', 'كود ريفورا الأصلي متاح بترخيص MIT وبدون ضمان، مع سريان إشعارات وشروط تراخيص الأطراف الخارجية.', 'هذه المعاينة لا تقدم سيرفر لعبة قابلًا للعب أو عناصر مدفوعة. تنزيل السورس لا يمنح حقوقًا في عميل اللعبة المملوك للناشر أو المكتبات والبيانات الخارجية الناقصة.'], path: '/blob/main/LICENSE'
    }
  };

  function safeHttps(value) {
    if (typeof value !== 'string') return null;
    try { const url = new URL(value); return url.protocol === 'https:' && !url.username && !url.password ? url.href : null; } catch { return null; }
  }
  const repository = (safeHttps(config.repositoryUrl) || defaultRepository).replace(/\/$/, '');
  document.querySelectorAll('[data-repo]').forEach(link => { link.href = repository; });
  document.querySelectorAll('[data-source-zip]').forEach(link => { link.href = `${repository}/archive/refs/heads/main.zip`; });
  let language = 'en';
  let activeWorld = 'explore';
  let activeStory = null;
  const dialog = document.getElementById('info-dialog');

  function renderWorld() {
    const world = worlds[activeWorld];
    const [kicker, title, body] = world[language];
    document.getElementById('world-kicker').textContent = kicker;
    document.getElementById('world-heading').textContent = title;
    document.getElementById('world-body').textContent = body;
    document.getElementById('world-number').textContent = world.number;
    document.getElementById('world-image').src = `./assets/${world.image}`;
    document.getElementById('world-image').alt = language === 'ar' ? 'تصور فني لعالم ريفورا' : 'Revora fantasy world artwork';
    document.getElementById('world-link').href = `${repository}/blob/main/server/docs/FEATURES.md`;
    document.querySelectorAll('[data-world]').forEach(button => { const selected = button.dataset.world === activeWorld; button.setAttribute('aria-selected', String(selected)); button.tabIndex = selected ? 0 : -1; if (selected) document.getElementById('world-panel').setAttribute('aria-labelledby', button.id); });
  }
  function renderStory() {
    if (!activeStory) return;
    const story = stories[activeStory];
    const [title, ...paragraphs] = story[language];
    document.getElementById('dialog-title').textContent = title;
    document.getElementById('dialog-body').replaceChildren(...paragraphs.map(text => { const paragraph = document.createElement('p'); paragraph.textContent = text; return paragraph; }));
    document.getElementById('dialog-link').href = `${repository}${story.path}`;
  }
  function applyLanguage(next) {
    language = next === 'ar' ? 'ar' : 'en';
    const dictionary = language === 'ar' ? arabic : english;
    document.documentElement.lang = language;
    document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
    document.querySelectorAll('[data-i18n]').forEach(element => { if (dictionary[element.dataset.i18n]) element.textContent = dictionary[element.dataset.i18n]; });
    const toggle = document.getElementById('language-toggle');
    toggle.textContent = language === 'en' ? 'العربية' : 'English';
    toggle.lang = language === 'en' ? 'ar' : 'en';
    toggle.setAttribute('aria-label', language === 'en' ? 'Switch to Arabic' : 'التبديل إلى الإنجليزية');
    document.querySelector('.dialog-close').setAttribute('aria-label', language === 'ar' ? 'إغلاق النافذة' : 'Close dialog');
    document.title = language === 'ar' ? 'ريفورا — اصنع أسطورتك' : 'Revora — Forge your own legend';
    try { localStorage.setItem('revora-language', language); } catch { /* Storage is optional. */ }
    renderWorld(); renderStory();
  }
  function openStory(name) { activeStory = name; renderStory(); if (!dialog.open) dialog.showModal(); }
  document.querySelectorAll('[data-story]').forEach(button => { button.addEventListener('click', () => openStory(button.dataset.story)); });
  document.querySelectorAll('[data-account]').forEach(button => { button.addEventListener('click', () => { const accountUrl = safeHttps(config.accountUrl); if (accountUrl) window.open(accountUrl, '_blank', 'noopener,noreferrer'); else openStory('account'); }); });
  document.querySelector('.dialog-close').addEventListener('click', () => dialog.close());
  dialog.addEventListener('click', event => { const rect = dialog.getBoundingClientRect(); if (event.target === dialog && (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom)) dialog.close(); });
  dialog.addEventListener('close', () => { activeStory = null; });
  document.getElementById('language-toggle').addEventListener('click', () => applyLanguage(language === 'en' ? 'ar' : 'en'));
  const tabs = [...document.querySelectorAll('[data-world]')];
  tabs.forEach((button, index) => {
    button.addEventListener('click', () => { activeWorld = button.dataset.world; renderWorld(); });
    button.addEventListener('keydown', event => {
      let next;
      if (event.key === 'Home') next = 0;
      if (event.key === 'End') next = tabs.length - 1;
      if (event.key === 'ArrowRight') next = (index + (language === 'ar' ? -1 : 1) + tabs.length) % tabs.length;
      if (event.key === 'ArrowLeft') next = (index + (language === 'ar' ? 1 : -1) + tabs.length) % tabs.length;
      if (next !== undefined) { event.preventDefault(); activeWorld = tabs[next].dataset.world; renderWorld(); tabs[next].focus(); }
    });
  });
  const menuToggle = document.getElementById('menu-toggle');
  const navigation = document.getElementById('main-nav');
  function closeMenu() { menuToggle.setAttribute('aria-expanded', 'false'); navigation.classList.remove('is-open'); }
  menuToggle.addEventListener('click', () => { const open = menuToggle.getAttribute('aria-expanded') !== 'true'; menuToggle.setAttribute('aria-expanded', String(open)); navigation.classList.toggle('is-open', open); });
  navigation.querySelectorAll('a').forEach(link => link.addEventListener('click', closeMenu));
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && navigation.classList.contains('is-open')) { closeMenu(); menuToggle.focus(); } });
  document.addEventListener('click', event => { if (!event.target.closest('.site-header')) closeMenu(); });
  let savedLanguage = 'en';
  try { savedLanguage = localStorage.getItem('revora-language') || 'en'; } catch { /* Defaults work without storage. */ }
  applyLanguage(savedLanguage);
})();
