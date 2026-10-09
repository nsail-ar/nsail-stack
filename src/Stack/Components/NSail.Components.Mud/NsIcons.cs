// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components;

/// <summary>Icon catalog for the generic UI vocabulary: pages and kits name icons through
/// this class, never through the vendor's. Products add their own catalog for domain icons.</summary>
// Material Symbols (Apache 2.0), drawn at the 20px optical size rather than a scaled 24.
// Symbols ships a "0 -960 960 960" viewBox, and an icon reaches the screen through whichever
// control was given it (a button's StartIcon, a nav link, a field adornment) — each renders
// its own svg fixed at "0 0 24 24", and none of them asks. So every entry carries the mapping
// itself: scale by 24/960 and drop back into the visible quadrant. Self-contained, one line,
// identical everywhere. Paste new icons the same way.
public static class NsIcons
{
    /// <summary>Material Symbols <c>home</c>.</summary>
    public static readonly Glyph Home =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M264-216h96v-240h240v240h96v-348L480-726 264-564v348Zm-72 72v-456l288-216 288 216v456H528v-240h-96v240H192Zm288-327Z"/></g>""");

    /// <summary>Material Symbols <c>search</c>.</summary>
    public static readonly Glyph Search =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M765-144 526-383q-30 22-65.79 34.5-35.79 12.5-76.18 12.5Q284-336 214-406t-70-170q0-100 70-170t170-70q100 0 170 70t70 170.03q0 40.39-12.5 76.18Q599-464 577-434l239 239-51 51ZM384-408q70 0 119-49t49-119q0-70-49-119t-119-49q-70 0-119 49t-49 119q0 70 49 119t119 49Z"/></g>""");

    /// <summary>Material Symbols <c>add</c>.</summary>
    public static readonly Glyph Add =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M444-444H240v-72h204v-204h72v204h204v72H516v204h-72v-204Z"/></g>""");

    /// <summary>Material Symbols <c>edit</c>.</summary>
    public static readonly Glyph Edit =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-216h51l375-375-51-51-375 375v51Zm-72 72v-153l498-498q11-11 23.84-16 12.83-5 27-5 14.16 0 27.16 5t24 16l51 51q11 11 16 24t5 26.54q0 14.45-5.02 27.54T795-642L297-144H144Zm600-549-51-51 51 51Zm-127.95 76.95L591-642l51 51-25.95-25.05Z"/></g>""");

    /// <summary>Material Symbols <c>delete</c>.</summary>
    public static readonly Glyph Delete =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M312-144q-29.7 0-50.85-21.15Q240-186.3 240-216v-480h-48v-72h192v-48h192v48h192v72h-48v479.57Q720-186 698.85-165T648-144H312Zm336-552H312v480h336v-480ZM384-288h72v-336h-72v336Zm120 0h72v-336h-72v336ZM312-696v480-480Z"/></g>""");

    /// <summary>Material Symbols <c>save</c>.</summary>
    public static readonly Glyph Save =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M816-672v456q0 29.7-21.15 50.85Q773.7-144 744-144H216q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h456l144 144Zm-72 30L642-744H216v528h528v-426ZM556.5-283.5Q588-315 588-360t-31.5-76.5Q525-468 480-468t-76.5 31.5Q372-405 372-360t31.5 76.5Q435-252 480-252t76.5-31.5ZM264-552h336v-144H264v144Zm-48-77v413-528 115Z"/></g>""");

    /// <summary>Material Symbols <c>close</c>.</summary>
    public static readonly Glyph Close =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m291-240-51-51 189-189-189-189 51-51 189 189 189-189 51 51-189 189 189 189-51 51-189-189-189 189Z"/></g>""");

    /// <summary>Material Symbols <c>check</c>.</summary>
    public static readonly Glyph Check =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M389-267 195-460l51-52 143 143 325-324 51 51-376 375Z"/></g>""");

    /// <summary>Material Symbols <c>settings</c>.</summary>
    public static readonly Glyph Settings =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m403-96-22-114q-23-9-44.5-21T296-259l-110 37-77-133 87-76q-2-12-3-24t-1-25q0-13 1-25t3-24l-87-76 77-133 110 37q19-16 40.5-28t44.5-21l22-114h154l22 114q23 9 44.5 21t40.5 28l110-37 77 133-87 76q2 12 3 24t1 25q0 13-1 25t-3 24l87 76-77 133-110-37q-19 16-40.5 28T579-210L557-96H403Zm59-72h36l19-99q38-7 71-26t57-48l96 32 18-30-76-67q6-17 9.5-35.5T696-480q0-20-3.5-38.5T683-554l76-67-18-30-96 32q-24-29-57-48t-71-26l-19-99h-36l-19 99q-38 7-71 26t-57 48l-96-32-18 30 76 67q-6 17-9.5 35.5T264-480q0 20 3.5 38.5T277-406l-76 67 18 30 96-32q24 29 57 48t71 26l19 99Zm18-168q60 0 102-42t42-102q0-60-42-102t-102-42q-60 0-102 42t-42 102q0 60 42 102t102 42Zm0-144Z"/></g>""");

    /// <summary>Material Symbols <c>palette</c>.</summary>
    public static readonly Glyph Palette =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-96q-79 0-149-30t-122.5-82.5Q156-261 126-331T96-480q0-80 30.5-149.5t84-122Q264-804 335.5-834T488-864q78 0 146.5 27T754-763q51 47 80.5 110T864-518q0 96-67 163t-163 67h-68q-8 0-14 5t-6 13q0 15 15 25t15 53q0 37-27 66.5T480-96Zm0-384Zm-173.5 18.5Q324-479 324-504t-17.5-42.5Q289-564 264-564t-42.5 17.5Q204-529 204-504t17.5 42.5Q239-444 264-444t42.5-17.5Zm120-144Q444-623 444-648t-17.5-42.5Q409-708 384-708t-42.5 17.5Q324-673 324-648t17.5 42.5Q359-588 384-588t42.5-17.5Zm192 0Q636-623 636-648t-17.5-42.5Q601-708 576-708t-42.5 17.5Q516-673 516-648t17.5 42.5Q551-588 576-588t42.5-17.5Zm120 144Q756-479 756-504t-17.5-42.5Q721-564 696-564t-42.5 17.5Q636-529 636-504t17.5 42.5Q671-444 696-444t42.5-17.5ZM480-168q11 0 17.5-8.5T504-192q0-16-15-28t-15-50q0-38 26.5-64t64.5-26h69q66 0 112-46t46-112q0-115-88.5-194.5T488-792q-134 0-227 91t-93 221q0 130 91 221t221 91Z"/></g>""");

    /// <summary>Material Symbols <c>image</c>.</summary>
    public static readonly Glyph Image =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.5Q144-187 144-216v-528q0-29 21.15-50.5T216-816h528q29.7 0 50.85 21.5Q816-773 816-744v528q0 29-21.15 50.5T744-144H216Zm0-72h528v-528H216v528Zm48-72h432L552-480 444-336l-72-96-108 144Zm-48 72v-528 528Z"/></g>""");

    /// <summary>Material Symbols <c>language</c>.</summary>
    public static readonly Glyph Language =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M331-126q-70-30-122.5-82.5T126-331q-30-70-30-149.5t30-149q30-69.5 82.5-122T331-834q70-30 149.5-30t149 30q69.5 30 122 82.5t82.5 122q30 69.5 30 149T834-331q-30 70-82.5 122.5t-122 82.5q-69.5 30-149 30T331-126Zm149-45q17-17 34-63.5T540-336H420q9 55 26 101.5t34 63.5Zm-91-10q-14-30-24.5-69T347-336H204q29 57 77 97.5T389-181Zm182 0q60-17 108-57.5t77-97.5H613q-7 47-17.5 86T571-181ZM177-408h161q-2-19-2.5-37.5T335-482q0-18 .5-35.5T338-552H177q-5 19-7 36.5t-2 35.5q0 18 2 35.5t7 36.5Zm234 0h138q2-20 2.5-37.5t.5-34.5q0-17-.5-35t-2.5-37H411q-2 19-2.5 37t-.5 35q0 17 .5 35t2.5 37Zm211 0h161q5-19 7-36.5t2-35.5q0-18-2-36t-7-36H622q2 19 2.5 37.5t.5 36.5q0 18-.5 35.5T622-408Zm-9-216h143q-29-57-77-97.5T571-779q14 30 24.5 69t17.5 86Zm-193 0h120q-9-55-26-101.5T480-789q-17 17-34 63.5T420-624Zm-216 0h143q7-47 17.5-86t24.5-69q-60 17-108 57.5T204-624Z"/></g>""");

    /// <summary>Material Symbols <c>tune</c>.</summary>
    public static readonly Glyph Tune =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M456-144v-240h72v84h288v72H528v84h-72Zm-312-84v-72h240v72H144Zm144-132v-84H144v-72h144v-84h72v240h-72Zm144-84v-72h384v72H432Zm144-132v-240h72v84h168v72H648v84h-72Zm-432-84v-72h384v72H144Z"/></g>""");

    /// <summary>Material Symbols <c>book_2</c>.</summary>
    public static readonly Glyph Directory =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M324-96q-54.69 0-93.34-38.66Q192-173.31 192-228v-504q0-54.69 38.66-93.34Q269.31-864 324-864h444v575q-25 0-42.5 17.91t-17.5 43.5q0 25.59 17.5 43.09Q743-167 768-167v71H324Zm-60-250q14-7 28.5-10.5T324-360h12v-432h-12q-25 0-42.5 17.5T264-732v386Zm144-14h288v-432H408v432Zm-144 14v-446 446Zm60 178h326q-7-14-10.5-28t-3.5-31.27q0-16.25 4-31.49Q644-274 651-288H324q-26 0-43 17.5T264-228q0 26 17 43t43 17Z"/></g>""");

    /// <summary>Material Symbols <c>groups</c>.</summary>
    public static readonly Glyph People =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M0-240v-59q0-51 45-80t123-29q15 0 30 1.5t30 4.5q-17 20-26.5 45t-9.5 50.56V-240H0Zm240 0v-61q0-27.86 14.5-50.93T293-387q44-22 91-33.5t95.53-11.5Q529-432 576-420.5t91 33.5q24 12 38.5 35.07T720-301v61H240Zm528 0v-67.37q0-26.95-9.5-50.79T732-402q17-3 31.5-4.5T792-408q78 0 123 29t45 80v59H768Zm-454-72h332q-7-17-59.5-32.5T480-360q-54 0-106.5 15.5T314-312ZM167.79-456Q138-456 117-477.03q-21-21.02-21-50.55Q96-558 117.03-579q21.02-21 50.55-21Q198-600 219-579.24t21 51.45Q240-498 219.24-477t-51.45 21Zm624 0Q762-456 741-477.03q-21-21.02-21-50.55Q720-558 741.03-579q21.02-21 50.55-21Q822-600 843-579.24t21 51.45Q864-498 843.24-477t-51.45 21ZM479.5-480q-49.5 0-84.5-35t-35-85q0-50 35-85t85-35q50 0 85 35t35 85.5q0 49.5-35 84.5t-85.5 35Zm.5-72q20.4 0 34.2-13.8Q528-579.6 528-600q0-20.4-13.8-34.2Q500.4-648 480-648q-20.4 0-34.2 13.8Q432-620.4 432-600q0 20.4 13.8 34.2Q459.6-552 480-552Zm0 240Zm0-288Z"/></g>""");

    /// <summary>Material Symbols <c>more_vert</c>.</summary>
    public static readonly Glyph More =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M479.79-192Q450-192 429-213.21t-21-51Q408-294 429.21-315t51-21Q510-336 531-314.79t21 51Q552-234 530.79-213t-51 21Zm0-216Q450-408 429-429.21t-21-51Q408-510 429.21-531t51-21Q510-552 531-530.79t21 51Q552-450 530.79-429t-51 21Zm0-216Q450-624 429-645.21t-21-51Q408-726 429.21-747t51-21Q510-768 531-746.79t21 51Q552-666 530.79-645t-51 21Z"/></g>""");

    /// <summary>Material Symbols <c>badge</c>.</summary>
    public static readonly Glyph Badge =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-96q-29.7 0-50.85-21.15Q96-138.3 96-168v-432q0-29.7 21.15-50.85Q138.3-672 168-672h216v-120q0-29.7 21.15-50.85Q426.3-864 456-864h48q29.7 0 50.85 21.15Q576-821.7 576-792v120h216q29.7 0 50.85 21.15Q864-629.7 864-600v432q0 29.7-21.15 50.85Q821.7-96 792-96H168Zm0-72h624v-432H576q0 30-21.15 51T504-528h-48q-29.7 0-50.85-21.15Q384-570.3 384-600H168v432Zm72-72h240v-23q0-17.63-9.5-32.67Q461-310.7 444-319q-20-8-40.5-12.5T360-336q-23 0-43.5 4.5T276-318.53q-17 7.53-26.5 22.66Q240-280.74 240-263v23Zm336-48h144v-72H576v72Zm-173.5-89.5Q420-395 420-420t-17.5-42.5Q385-480 360-480t-42.5 17.5Q300-445 300-420t17.5 42.5Q335-360 360-360t42.5-17.5ZM576-408h144v-72H576v72ZM456-600h48v-192h-48v192Zm24 216Z"/></g>""");

    /// <summary>Material Symbols <c>key</c>.</summary>
    public static readonly Glyph Key =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M220-412q-28-28-28-68t28-68q28-28 68-28t68 28q28 28 28 68t-28 68q-28 28-68 28t-68-28Zm68 172q-100 0-170-70T48-480q0-100 70-170t170-70q65 0 120 32.5t88 87.5h344l120 120-180 168-84-60-72 60-96-72h-20q-24 68-85.5 106T288-240Zm0-72q63 0 111-40.5T454-456h98l70 52 71-59 81 58 82-76-46-47H449q-19-53-62.5-86.5T288-648q-70 0-119 49t-49 119q0 70 49 119t119 49Z"/></g>""");

    /// <summary>Material Symbols <c>lock_open</c>.</summary>
    public static readonly Glyph LockOpen =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M264-624h336v-96q0-50-35-85t-85-35q-50 0-85 35t-35 85h-72q0-80 56.23-136 56.22-56 136-56Q560-912 616-855.84q56 56.16 56 135.84v96h24q29.7 0 50.85 21.15Q768-581.7 768-552v384q0 29.7-21.16 50.85Q725.68-96 695.96-96H263.72Q234-96 213-117.15T192-168v-384q0-29.7 21.15-50.85Q234.3-624 264-624Zm0 456h432v-384H264v384Zm267-141.21q21-21.21 21-51T530.79-411q-21.21-21-51-21T429-410.79q-21 21.21-21 51T429.21-309q21.21 21 51 21T531-309.21ZM264-168v-384 384Z"/></g>""");

    /// <summary>Material Symbols <c>link</c>.</summary>
    public static readonly Glyph Link =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M432-288H288q-79.68 0-135.84-56.23Q96-400.45 96-480.23 96-560 152.16-616q56.16-56 135.84-56h144v72H288q-50 0-85 35t-35 85q0 50 35 85t85 35h144v72Zm-96-156v-72h288v72H336Zm192 156v-72h144q50 0 85-35t35-85q0-50-35-85t-85-35H528v-72h144q79.68 0 135.84 56.23 56.16 56.22 56.16 136Q864-400 807.84-344 751.68-288 672-288H528Z"/></g>""");

    /// <summary>Material Symbols <c>card_membership</c>.</summary>
    public static readonly Glyph Membership =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-432v72h624v-72H168Zm0-432h624q29.7 0 50.85 21.15Q864-821.7 864-792v432q0 29.7-21.15 50.85Q821.7-288 792-288H624v192l-144-96-144 96v-192H168q-29.7 0-50.85-21.15Q96-330.3 96-360v-432q0-29.7 21.15-50.85Q138.3-864 168-864Zm0 336h624v-264H168v264Zm0 168v-432 432Z"/></g>""");

    /// <summary>Material Symbols <c>hub</c>.</summary>
    public static readonly Glyph Relationship =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M155-83q-35-35-35-85t35-85q35-35 85-35 14 0 28 3.5t27 9.5l55-69q-29-31-42.5-70.5T302-496l-76-23q-15 29-44 46t-62 17q-50 0-85-35T0-576q0-51 34.5-85.5T120-696q51 0 85.5 34.5T240-576v11l76 23q20-42 57-70t83-35v-75q-42-8-69-41t-27-77q0-50 35-85t85-35q50 0 85 35t35 85q0 44-27 77t-69 41v75q47 6 83.5 34t56.5 71l76-23v-11q0-50 35-85t85-35q50 0 85 35t35 85q0 50-35 85t-85 35q-33 0-62-16.5T734-519l-76 23q8 42-5.5 81.5T610-344l55 69q13-6 27-9.5t28-3.5q50 0 85 35t35 85q0 50-35 85t-85 35q-50 0-85-35t-35-85q0-21 7.5-40.5T628-245l-55-68q-43 26-93 26t-93-26l-55 68q13 17 20.5 36.5T360-168q0 50-35 85t-85 35q-50 0-85-35Zm-35-445q20 0 34-14t14-34q0-20-14-34t-34-14q-20 0-34 14t-14 34q0 20 14 34t34 14Zm154 394q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14Zm240-672q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14Zm-34 446q45 0 76.5-31.5T588-468q0-45-31.5-76.5T480-576q-45 0-76.5 31.5T372-468q0 45 31.5 76.5T480-360Zm274 226q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14Zm120-408q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14ZM480-840ZM120-576Zm360 108Zm360-108ZM240-168Zm480 0Z"/></g>""");

    /// <summary>Material Symbols <c>arrow_drop_down</c>.</summary>
    public static readonly Glyph DropDown =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-384 288-576h384L480-384Z"/></g>""");

    /// <summary>Material Symbols <c>chevron_right</c>.</summary>
    public static readonly Glyph ChevronRight =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M522-480 333-669l51-51 240 240-240 240-51-51 189-189Z"/></g>""");

    /// <summary>Material Symbols <c>check_box</c>.</summary>
    public static readonly Glyph CheckBoxOn =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m429-336 238-237-51-51-187 186-85-84-51 51 136 135ZM216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm0-72h528v-528H216v528Zm0-528v528-528Z"/></g>""");

    /// <summary>Material Symbols <c>check_box_outline_blank</c>.</summary>
    public static readonly Glyph CheckBoxOff =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm0-72h528v-528H216v528Z"/></g>""");

    /// <summary>Material Symbols <c>indeterminate_check_box</c>.</summary>
    public static readonly Glyph CheckBoxSome =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M336-444h288v-72H336v72ZM216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm0-72h528v-528H216v528Zm0-528v528-528Z"/></g>""");

    /// <summary>Material Symbols <c>apartment</c>.</summary>
    public static readonly Glyph Business =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M144-144v-528h144v-144h360v288h168v384H528v-144h-96v144H144Zm72-72h72v-72h-72v72Zm0-156h72v-72h-72v72Zm0-156h72v-72h-72v72Zm144 156h72v-72h-72v72Zm0-156h72v-72h-72v72Zm0-144h72v-72h-72v72Zm144 300h72v-72h-72v72Zm0-156h72v-72h-72v72Zm0-144h72v-72h-72v72Zm168 456h72v-72h-72v72Zm0-156h72v-72h-72v72Z"/></g>""");

    /// <summary>Material Symbols <c>account_balance</c>.</summary>
    public static readonly Glyph Accounting =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M192-264v-312h72v312h-72Zm252 0v-312h72v312h-72ZM96-144v-72h768v72H96Zm600-120v-312h72v312h-72ZM96-624v-96l384-192 384 192v96H96Zm113-72h542-542Zm0 0h542L480-831 209-696Z"/></g>""");

    /// <summary>Material Symbols <c>account_tree</c> — accounts as the structure they form,
    /// told apart from the <c>account_balance</c> that stands for accounting itself.</summary>
    public static readonly Glyph Accounts =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M624-144v-108H444v-384H336v108H96v-288h240v108h288v-108h240v288H624v-108H516v312h108v-108h240v288H624ZM168-744v144-144Zm528 384v144-144Zm0-384v144-144Zm0 144h96v-144h-96v144Zm0 384h96v-144h-96v144ZM168-600h96v-144h-96v144Z"/></g>""");

    /// <summary>Material Symbols <c>menu_book</c>.</summary>
    public static readonly Glyph Book =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M564-578v-76q31-10 64-14.5t68-4.5q23 0 46.5 2.5T792-663v74q-30-7-53-10t-43-3q-34 0-67 6t-65 18Zm0 236v-76q28-9 60-14.5t72-5.5q27 0 50.5 3t45.5 8v74q-30-7-53-10t-43-3q-34 0-67 6t-65 18Zm0-118v-76q32-10 65.5-15t66.5-5q27 0 50.5 3t45.5 8v74q-26-7-49.5-10t-46.5-3q-32 0-64.5 6T564-460ZM264-288q47 0 92 12t88 30v-454q-42-22-87-33t-93-11q-37 0-73.5 6.5T120-716v452q35-13 71-18.5t73-5.5Zm252 42q43-20 88-31t92-11q37 0 73.5 4.5T840-264v-452q-35-13-71-20.5t-73-7.5q-48 0-93 11t-87 33v454Zm-36 102q-49-32-103-52t-113-20q-38 0-76 7.5T115-186q-24 10-45.5-3.5T48-229v-503q0-14 7.5-26T76-776q45-20 92-30t96-10q57 0 111.5 13.5T480-762q51-26 105-40t111-14q49 0 96 10t92 30q13 6 21 18t8 26v503q0 25-15.5 40t-32.5 7q-40-18-82.5-26t-86.5-8q-59 0-113 20t-103 52ZM283-495Z"/></g>""");

    /// <summary>Material Symbols <c>receipt_long</c>.</summary>
    public static readonly Glyph Receipt =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M240-96q-46 0-71-24.5T144-192v-144h96v-528l57.6 58 57.6-58 57.6 58 57.6-58 57.6 58 57.6-58 57.6 58 57.6-58 57.6 58 57.6-58v660q0 47-31 77.5T708-96H240Zm468-72q16 0 26-9.5t10-26.5v-540H312v408h360v132q0 17 10 26.5t26 9.5ZM360-600v-72h216v72H360Zm0 120v-72h216v72H360Zm300-120q-14 0-25-10.29t-11-25.5q0-15.21 11-25.71t25.5-10.5q14.5 0 25 10.29t10.5 25.5q0 15.21-10.35 25.71T660-600Zm0 120q-14 0-25-10.29t-11-25.5q0-15.21 11-25.71t25.5-10.5q14.5 0 25 10.29t10.5 25.5q0 15.21-10.35 25.71T660-480ZM240-168h360v-96H216v72q0 17 3.5 20.5T240-168Zm-24 0v-96 96Z"/></g>""");

    /// <summary>Material Symbols <c>shopping_cart</c> — the buying side of a trade, where
    /// <c>receipt_long</c> is the paper either side produces.</summary>
    public static readonly Glyph Cart =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M213-117.21q-21-21.21-21-51T213.21-219q21.21-21 51-21T315-218.79q21 21.21 21 51T314.79-117q-21.21 21-51 21T213-117.21Zm432 0q-21-21.21-21-51T645.21-219q21.21-21 51-21T747-218.79q21 21.21 21 51T746.79-117q-21.21 21-51 21T645-117.21ZM253-696l83 192h301l82-192H253Zm-31-72h570q14 0 20.5 11t1.5 23L702.63-476.14Q694-456 676.5-444T637-432H317l-42 72h493v72H276q-43 0-63.5-36.15-20.5-36.16.5-71.85l52-90-131-306H48v-72h133l41 96Zm114 264h301-301Z"/></g>""");

    /// <summary>Material Symbols <c>storefront</c> — the shop as the place it sells from, the
    /// mirror of <c>shopping_cart</c>.</summary>
    public static readonly Glyph Storefront =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M816-504v287.64q0 29.85-21.15 51.1Q773.7-144 744-144H216q-29.7 0-50.85-21.5Q144-187 144-216v-289q-33-23-41.5-59t.5-76l46-128q5-24 23-36t43.67-12h528.66q25.67 0 43.17 11t23.5 37l46 128q9 40 0 76t-41 60Zm-248-48q21 0 35.5-14t12.5-34l-24-144h-72v143.62Q520-580 534-566q14 14 34 14Zm-176.5 0q20.5 0 34.5-14t14-34.38V-744h-72l-24 144q-2 20 12.5 34t35 14ZM216-552q18 0 31.5-11t15.5-28l25-153h-72l-45 128q-8 23 6 43.5t39 20.5Zm528 0q25 0 39.5-20.5T790-616l-46-128h-72l25 153q2 17 16 28t31 11ZM216-216h528v-264q-25 0-47.5-9.5T656-519q-18 20-40.36 29.5T568-480q-25.18 0-47.09-10Q499-500 480-519q-17 19-40 29t-48 10q-25 0-47-8.5T304-519q-22.02 21.55-43.51 30.28Q239-480 216-480v264Zm528 0H216h.5-.71 528.28H743h1Z"/></g>""");

    /// <summary>Material Symbols <c>payments</c>.</summary>
    public static readonly Glyph Payments =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M552-432q-50 0-85-35t-35-85q0-50 35-85t85-35q50 0 85 35t35 85q0 50-35 85t-85 35Zm-288 96q-29.7 0-50.85-21.17Q192-378.33 192-408.06v-288.22Q192-726 213.15-747T264-768h576q29.7 0 50.85 21.17Q912-725.67 912-695.94v288.22Q912-378 890.85-357T840-336H264Zm72-72h432q0-30 21.15-51.12 21.15-21.11 50.85-21.11V-624q-29.7 0-50.85-21.15Q768-666.3 768-696H336q0 30-21.15 51.12-21.15 21.11-50.85 21.11V-480q29.7 0 50.85 21.15Q336-437.7 336-408Zm456 216H120q-29.7 0-50.85-21.15Q48-234.3 48-264v-408h72v408h672v72ZM264-408v-288 288Z"/></g>""");

    /// <summary>Material Symbols <c>price_check</c> — money come IN, where <c>payments</c> is
    /// money going out.</summary>
    public static readonly Glyph Collect =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M336-360v-48h-96v-72h192v-72H288q-20.4 0-34.2-13.8Q240-579.6 240-600v-120q0-20.4 13.8-34.2Q267.6-768 288-768h48v-48h72v48h96v72H312v72h144q20.4 0 34.2 13.8Q504-596.4 504-576v120q0 20.4-13.8 34.2Q476.4-408 456-408h-48v48h-72Zm237 216L415-302l51-51 107 107 192-192 51 51-243 243Z"/></g>""");

    /// <summary>Material Symbols <c>account_balance_wallet</c> — what one party's running
    /// balance comes to.</summary>
    public static readonly Glyph Wallet =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-223v7-528 521Zm0 79q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v98h-72v-98H216v528h528v-99h72v99q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm288-144q-29.7 0-50.85-21.15Q432-330.3 432-360v-240q0-29.7 21.15-50.85Q474.3-672 504-672h288q29.7 0 50.85 21.15Q864-629.7 864-600v240q0 29.7-21.15 50.85Q821.7-288 792-288H504Zm288-72v-240H504v240h288Zm-101.5-77.5Q708-455 708-480t-17.5-42.5Q673-540 648-540t-42.5 17.5Q588-505 588-480t17.5 42.5Q623-420 648-420t42.5-17.5Z"/></g>""");

    /// <summary>Material Symbols <c>credit_card</c>.</summary>
    public static readonly Glyph Card =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M864-696v432q0 29-21.15 50.5T792-192H168q-29 0-50.5-21.5T96-264v-432q0-29 21.5-50.5T168-768h624q29.7 0 50.85 21.5Q864-725 864-696Zm-696 72h624v-72H168v72Zm0 144v216h624v-216H168Zm0 216v-432 432Z"/></g>""");

    /// <summary>Material Symbols <c>point_of_sale</c>.</summary>
    public static readonly Glyph Till =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M288-648q-29.7 0-50.85-21.19Q216-690.37 216-720.12v-72.13Q216-822 237.15-843T288-864h384q29.7 0 50.85 21.19Q744-821.63 744-791.88v72.13Q744-690 722.85-669T672-648H288Zm0-72h384v-72H288v72ZM168-96q-29.7 0-50.85-21.15Q96-138.3 96-168v-24h768v24q0 29.7-21.15 50.85Q821.7-96 792-96H168ZM96-240l149-318q8.8-19.66 26.4-30.83Q289-600 309.82-600h340.36q20.82 0 38.42 11.17T715-558l149 318H96Zm264-72h24q9.6 0 16.8-7 7.2-7 7.2-17t-7.2-17q-7.2-7-16.8-7h-24q-9.6 0-16.8 7-7.2 7-7.2 17t7.2 17q7.2 7 16.8 7Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7 7.2-7 7.2-17t-7.2-17q-7.2-7-16.8-7h-24q-9.6 0-16.8 7-7.2 7-7.2 17t7.2 17q7.2 7 16.8 7Zm108 168h24q9.6 0 16.8-7 7.2-7 7.2-17t-7.2-17q-7.2-7-16.8-7h-24q-9.6 0-16.8 7-7.2 7-7.2 17t7.2 17q7.2 7 16.8 7Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7 7.2-7 7.2-17t-7.2-17q-7.2-7-16.8-7h-24q-9.6 0-16.8 7-7.2 7-7.2 17t7.2 17q7.2 7 16.8 7Zm108 168h24q9.6 0 16.8-7 7.2-7 7.2-17t-7.2-17q-7.2-7-16.8-7h-24q-9.6 0-16.8 7-7.2 7-7.2 17t7.2 17q7.2 7 16.8 7Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7 7.2-7 7.2-17t-7.2-17q-7.2-7-16.8-7h-24q-9.6 0-16.8 7-7.2 7-7.2 17t7.2 17q7.2 7 16.8 7Z"/></g>""");

    /// <summary>Material Symbols <c>currency_exchange</c>.</summary>
    public static readonly Glyph Exchange =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-48q-113 0-207.5-52.5T120-241v121H48v-240h240v72H175q48 76 128 122t177 46q75 0 140.5-28.5t114-77q48.5-48.5 77-114T840-480h72q0 90-34 168.5t-92.5 137Q727-116 648.5-82T480-48Zm-33-168v-48q-21-5-58.5-27T333-376l63-26q2 6 20 42.5t70 36.5q26 0 50.5-14.5T561-384q0-27-20.5-43.5T475-460q-31-11-78.5-35.5T349-585q0-3 13-49t86-62v-48h66v47q53 9 74.5 40t25.5 44l-59 25q-3-10-19-30t-53-20q-20 0-44 11.5T415-586q0 27 24.5 41t75.5 31q67 23 89.5 56.5T627-384q0 37-15 60t-34.5 36.5Q558-274 539.5-269t-26.5 6v47h-66ZM48-480q0-90 34-168.5t92.5-137Q233-844 311.5-878T480-912q113 0 207.5 52.5T840-719v-121h72v240H672v-72h113q-48-76-128-122t-177-46q-75 0-140.5 28.5t-114 77q-48.5 48.5-77 114T120-480H48Z"/></g>""");

    /// <summary>Material Symbols <c>inventory_2</c>.</summary>
    public static readonly Glyph Products =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-96q-29.7 0-50.85-21.15Q144-138.3 144-168v-412q-21-8-34.5-26.5T96-648v-144q0-29.7 21.15-50.85Q138.3-864 168-864h624q29.7 0 50.85 21.15Q864-821.7 864-792v144q0 23-13.5 41.5T816-580v411.86Q816-138 794.85-117T744-96H216Zm0-480v408h528v-408H216Zm-48-72h624v-144H168v144Zm216 240h192v-72H384v72Zm96 36Z"/></g>""");

    /// <summary>Material Symbols <c>category</c>.</summary>
    public static readonly Glyph Catalog =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m276-528 204-336 204 336H276ZM696-96q-70 0-119-49t-49-119q0-70 49-119t119-49q70 0 119 49t49 119q0 70-49 119T696-96Zm-552-24v-288h288v288H144Zm551.77-48Q736-168 764-195.77q28-27.78 28-68Q792-304 764.23-332q-27.78-28-68-28Q656-360 628-332.23q-28 27.78-28 68Q600-224 627.77-196q27.78 28 68 28ZM216-192h144v-144H216v144Zm188-408h152l-76-125-76 125Zm76 0ZM360-336Zm331 67Z"/></g>""");

    /// <summary>Material Symbols <c>label</c> — the name a thing is sold under, where
    /// <c>category</c> is the shelf it sits on.</summary>
    public static readonly Glyph Tag =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-192q-29.7 0-50.85-21.16Q96-234.32 96-264.04v-432.24Q96-726 117.15-747T168-768h420q16.85 0 31.92 7.5Q635-753 646-739l194 259-194 259q-11 14-26.08 21.5Q604.85-192 588-192H168Zm0-72h420l162-216-162-216H168v432Zm210-216Z"/></g>""");

    /// <summary>Material Symbols <c>file_copy</c> — the form a document is stamped out of,
    /// which no single <c>description</c> page says.</summary>
    public static readonly Glyph Template =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M744-192H312q-29 0-50.5-21.5T240-264v-576q0-29 21.5-50.5T312-912h312l192 192v456q0 29-21.5 50.5T744-192ZM576-672v-168H312v576h432v-408H576ZM168-48q-29 0-50.5-21.5T96-120v-552h72v552h456v72H168Zm144-792v195-195 576-576Z"/></g>""");

    /// <summary>Material Symbols <c>person</c>.</summary>
    public static readonly Glyph Person =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M378-522q-42-42-42-102t42-102q42-42 102-42t102 42q42 42 42 102t-42 102q-42 42-102 42t-102-42ZM192-192v-96q0-23 12.5-43.5T239-366q55-32 116.29-49 61.29-17 124.5-17t124.71 17Q666-398 721-366q22 13 34.5 34t12.5 44v96H192Zm72-72h432v-24q0-5.18-3.03-9.41-3.02-4.24-7.97-6.59-46-28-98-42t-107-14q-55 0-107 14t-98 42q-5 4-8 7.72-3 3.73-3 8.28v24Zm267-309.21q21-21.21 21-51T530.79-675q-21.21-21-51-21T429-674.79q-21 21.21-21 51T429.21-573q21.21 21 51 21T531-573.21ZM480-624Zm0 360Z"/></g>""");

    /// <summary>Material Symbols <c>person_add</c>.</summary>
    public static readonly Glyph PersonAdd =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M708-432v-84h-84v-72h84v-84h72v84h84v72h-84v84h-72Zm-426-90q-42-42-42-102t42-102q42-42 102-42t102 42q42 42 42 102t-42 102q-42 42-102 42t-102-42ZM96-192v-92q0-25.78 12.5-47.39T143-366q55-32 116-49t125-17q64 0 125 17t116 49q22 13 34.5 34.61T672-284v92H96Zm72-72h432v-20q0-6.47-3.03-11.76-3.02-5.3-7.97-8.24-47-27-99-41.5T384-360q-54 0-106 14.5T179-304q-4.95 2.94-7.98 8.24Q168-290.47 168-284v20Zm267-309.21q21-21.21 21-51T434.79-675q-21.21-21-51-21T333-674.79q-21 21.21-21 51T333.21-573q21.21 21 51 21T435-573.21ZM384-625Zm0 361Z"/></g>""");

    /// <summary>Material Symbols <c>print</c>.</summary>
    public static readonly Glyph Print =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M648-624v-120H312v120h-72v-192h480v192h-72Zm-480 72h625-625Zm539.79 96q15.21 0 25.71-10.29t10.5-25.5q0-15.21-10.29-25.71t-25.5-10.5q-15.21 0-25.71 10.29t-10.5 25.5q0 15.21 10.29 25.71t25.5 10.5ZM648-216v-144H312v144h336Zm72 72H240v-144H96v-240q0-40 28-68t68-28h576q40 0 68 28t28 68v240H720v144Zm73-216v-153.67Q793-530 781-541t-28-11H206q-16.15 0-27.07 11.04Q168-529.92 168-513.6V-360h72v-72h480v72h73Z"/></g>""");

    /// <summary>Material Symbols <c>logout</c>.</summary>
    public static readonly Glyph Logout =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h264v72H216v528h264v72H216Zm432-168-51-51 81-81H384v-72h294l-81-81 51-51 168 168-168 168Z"/></g>""");

    /// <summary>Material Symbols <c>undo</c>.</summary>
    public static readonly Glyph Undo =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M288-192v-72h288q50 0 85-35t35-85q0-50-35-85t-85-35H330l93 93-51 51-180-180 180-180 51 51-93 93h246q80 0 136 56t56 136q0 80-56 136t-136 56H288Z"/></g>""");

    /// <summary>Material Symbols <c>calendar_month</c>.</summary>
    public static readonly Glyph Calendar =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-96q-29.7 0-50.85-21.5Q144-139 144-168v-528q0-29 21.15-50.5T216-768h72v-96h72v96h240v-96h72v96h72q29.7 0 50.85 21.5Q816-725 816-696v528q0 29-21.15 50.5T744-96H216Zm0-72h528v-360H216v360Zm0-432h528v-96H216v96Zm0 0v-96 96Zm264.21 216q-15.21 0-25.71-10.29t-10.5-25.5q0-15.21 10.29-25.71t25.5-10.5q15.21 0 25.71 10.29t10.5 25.5q0 15.21-10.29 25.71t-25.5 10.5ZM298.5-394.29q-10.5-10.29-10.5-25.5t10.29-25.71q10.29-10.5 25.5-10.5t25.71 10.29q10.5 10.29 10.5 25.5t-10.29 25.71q-10.29 10.5-25.5 10.5t-25.71-10.29ZM636.21-384q-15.21 0-25.71-10.29t-10.5-25.5q0-15.21 10.29-25.71t25.5-10.5q15.21 0 25.71 10.29t10.5 25.5q0 15.21-10.29 25.71t-25.5 10.5Zm-156 144q-15.21 0-25.71-10.29t-10.5-25.5q0-15.21 10.29-25.71t25.5-10.5q15.21 0 25.71 10.29t10.5 25.5q0 15.21-10.29 25.71t-25.5 10.5ZM298.5-250.29q-10.5-10.29-10.5-25.5t10.29-25.71q10.29-10.5 25.5-10.5t25.71 10.29q10.5 10.29 10.5 25.5t-10.29 25.71q-10.29 10.5-25.5 10.5t-25.71-10.29ZM636.21-240q-15.21 0-25.71-10.29t-10.5-25.5q0-15.21 10.29-25.71t25.5-10.5q15.21 0 25.71 10.29t10.5 25.5q0 15.21-10.29 25.71t-25.5 10.5Z"/></g>""");

    /// <summary>Material Symbols <c>schedule</c>.</summary>
    public static readonly Glyph Schedule =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m614-310 51-51-149-149v-210h-72v240l170 170ZM480-96q-79.38 0-149.19-30T208.5-208.5Q156-261 126-330.96t-30-149.5Q96-560 126-630q30-70 82.5-122t122.46-82q69.96-30 149.5-30t149.55 30.24q70 30.24 121.79 82.08 51.78 51.84 81.99 121.92Q864-559.68 864-480q0 79.38-30 149.19T752-208.5Q700-156 629.87-126T480-96Zm0-384Zm.48 312q129.47 0 220.5-91.5Q792-351 792-480.48q0-129.47-91.02-220.5Q609.95-792 480.48-792 351-792 259.5-700.98 168-609.95 168-480.48 168-351 259.5-259.5T480.48-168Z"/></g>""");

    /// <summary>Material Symbols <c>event_upcoming</c> — the days ahead, told apart from
    /// Calendar (the agenda itself) and Today (the day on screen).</summary>
    public static readonly Glyph Upcoming =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M576-96v-72h168v-360H216v216h-72v-384q0-29 21.15-50.5T216-768h72v-96h72v96h240v-96h72v96h72q29 0 50.5 21.5T816-696v528q0 29-21.5 50.5T744-96H576ZM363-48l-51-51 56-57H96v-72h272l-56-57 51-51 141 144L363-48ZM216-600h528v-96H216v96Zm0 0v-96 96Z"/></g>""");

    /// <summary>Material Symbols <c>today</c>.</summary>
    public static readonly Glyph Today =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M316-291.77q-28-27.78-28-68Q288-400 315.77-428q27.78-28 68-28Q424-456 452-428.23q28 27.78 28 68Q480-320 452.23-292q-27.78 28-68 28Q344-264 316-291.77ZM216-96q-29.7 0-50.85-21.5Q144-139 144-168v-528q0-29 21.15-50.5T216-768h72v-96h72v96h240v-96h72v96h72q29.7 0 50.85 21.5Q816-725 816-696v528q0 29-21.15 50.5T744-96H216Zm0-72h528v-360H216v360Zm0-432h528v-96H216v96Zm0 0v-96 96Z"/></g>""");

    /// <summary>Material Symbols <c>download</c>.</summary>
    public static readonly Glyph Download =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-336 288-528l51-51 105 105v-342h72v342l105-105 51 51-192 192ZM263.72-192Q234-192 213-213.15T192-264v-72h72v72h432v-72h72v72q0 29.7-21.16 50.85Q725.68-192 695.96-192H263.72Z"/></g>""");

    /// <summary>Material Symbols <c>arrow_upward</c>.</summary>
    public static readonly Glyph MoveUp =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M444-192v-438L243-429l-51-51 288-288 288 288-51 51-201-201v438h-72Z"/></g>""");

    /// <summary>Material Symbols <c>arrow_downward</c>.</summary>
    public static readonly Glyph MoveDown =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M444-768v438L243-531l-51 51 288 288 288-288-51-51-201 201v-438h-72Z"/></g>""");

    /// <summary>Material Symbols <c>star</c>, filled.</summary>
    public static readonly Glyph Star =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m352-293 128-76 129 76-34-144 111-95-147-13-59-137-59 137-147 13 112 95-34 144ZM243-144l63-266L96-589l276-24 108-251 108 252 276 23-210 179 63 266-237-141-237 141Zm237-333Z"/></g>""");

    /// <summary>Material Symbols <c>location_on</c>.</summary>
    public static readonly Glyph Location =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M531-501q21-21 21-51t-21-51q-21-21-51-21t-51 21q-21 21-21 51t21 51q21 21 51 21t51-21Zm-51 310q119-107 179.5-197T720-549q0-105-68.5-174T480-792q-103 0-171.5 69T240-549q0 71 60.5 161T480-191Zm0 95Q323-227 245.5-339.5T168-549q0-134 89-224.5T480-864q133 0 222.5 90.5T792-549q0 97-77 209T480-96Zm0-456Z"/></g>""");

    /// <summary>Material Symbols <c>mail</c>.</summary>
    public static readonly Glyph Mail =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-192q-29.7 0-50.85-21.16Q96-234.32 96-264.04v-432.24Q96-726 117.15-747T168-768h624q29.7 0 50.85 21.16Q864-725.68 864-695.96v432.24Q864-234 842.85-213T792-192H168Zm312-240L168-611v347h624v-347L480-432Zm0-85 312-179H168l312 179Zm-312-94v-85 432-347Z"/></g>""");

    /// <summary>Material Symbols <c>send</c>.</summary>
    public static readonly Glyph Send =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M144-192v-576l720 288-720 288Zm72-107 454-181-454-181v109l216 72-216 72v109Zm0 0v-362 362Z"/></g>""");

    /// <summary>Material Symbols <c>move_to_inbox</c> — it arrives: goods landing in a store, a
    /// job coming back from a workshop. Send's counterpart, and generic for the same reason.</summary>
    public static readonly Glyph Receive =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29 0-50.5-21.5T144-216v-528q0-29.7 21.5-50.85Q187-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29-21.15 50.5T744-144H216Zm0-72h528v-144H632q-23 43-63.5 69.5T480-264q-49 0-89.5-26T328-360H216v144Zm332-148q28-28 28-68h168v-312H216v312h168q0 40 28 68t68 28q40 0 68-28Zm-68-68L336-576l51-51 57 57v-126h72v126l57-57 51 51-144 144ZM216-216h528-528Z"/></g>""");

    /// <summary>Material Symbols <c>open_in_new</c> — opens somewhere else, not a plain link.</summary>
    public static readonly Glyph OpenExternal =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h264v72H216v528h528v-264h72v264q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm171-192-51-51 357-357H576v-72h240v240h-72v-117L387-336Z"/></g>""");

    /// <summary>Material Symbols <c>phone_in_talk</c>.</summary>
    public static readonly Glyph Call =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M744-481q0-109-77.5-186.5T480-745v-72q70 0 131.13 26.6 61.14 26.6 106.4 71.87 45.27 45.26 71.87 106.4Q816-551 816-481h-72Zm-144 0q0-50-35-85t-85-35v-72q80 0 136 56.16T672-481h-72Zm163 336q-121-9-229.5-59.5T339-341q-86-86-136-194.5T144-765q-2-21 12.29-36.5Q170.57-817 192-817h136q17 0 29.5 10.5T374-780l24 107q2 13-1.5 25T385-628l-97 98q20 38 46 73t57.97 65.98Q422-361 456-335.5q34 25.5 72 45.5l99-96q8-8 20-11.5t25-1.5l107 23q17 5 27 17.5t10 29.5v136q0 21.43-16 35.71Q784-143 763-145ZM255-600l70-70-17.16-75H218q5 38 14 74t23 71Zm344 344q35.1 14.24 71.55 22.62Q707-225 744-220v-90l-75-16-70 70ZM255-600Zm344 344Z"/></g>""");

    /// <summary>Material Symbols <c>description</c>.</summary>
    public static readonly Glyph Note =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M336-240h288v-72H336v72Zm0-144h288v-72H336v72ZM263.72-96Q234-96 213-117.15T192-168v-624q0-29.7 21.15-50.85Q234.3-864 264-864h312l192 192v504q0 29.7-21.16 50.85Q725.68-96 695.96-96H263.72ZM528-624v-168H264v624h432v-456H528ZM264-792v189-189 624-624Z"/></g>""");

    /// <summary>Material Symbols <c>edit_note</c>.</summary>
    public static readonly Glyph Amend =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M192-396v-72h288v72H192Zm0-150v-72h432v72H192Zm0-150v-72h432v72H192Zm336 504v-113l210-209q7.26-7.41 16.13-10.71Q763-528 771.76-528q9.55 0 18.31 3.5Q798.83-521 806-514l44 45q6.59 7.26 10.29 16.13Q864-444 864-435.24t-3.29 17.92q-3.3 9.15-10.71 16.32L641-192H528Zm288-243-45-45 45 45ZM576-240h45l115-115-22-23-22-22-116 115v45Zm138-138-22-22 44 45-22-23Z"/></g>""");

    /// <summary>Material Symbols <c>stethoscope</c>.</summary>
    public static readonly Glyph Medical =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M533-96q-97.62 0-166.31-68.98Q298-233.97 298-332v-30q-85-11-143.5-74.5T96-588v-228h120v-48h72v168h-72v-48h-48v156.46q0 64.54 45.5 110.04T324-432q65 0 110.5-45.5T480-587.54V-744h-48v48h-72v-168h72v48h120v228q0 84.35-51.5 146.67Q449-379 370-364v33q0 68.33 47.56 116.17Q465.12-167 533.06-167t115.44-47.83Q696-262.67 696-331v-59.37Q659-401 635.5-432T612-504q0-50 35-85t85-35q50 0 85 35t35 85q0 41-23.5 72T768-390v58q0 98.03-68.69 167.02Q630.62-96 533-96Zm233-374q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14Zm-34-34Z"/></g>""");

    /// <summary>Material Symbols <c>health_and_safety</c>.</summary>
    public static readonly Glyph Coverage =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M432-360h96v-96h96v-96h-96v-96h-96v96h-96v96h96v96Zm48 264q-135-33-223.5-152.84Q168-368.69 168-515v-229l312-120 312 120v229q0 146.31-88.5 266.16Q615-129 480-96Zm0-75q104-32.25 172-129t68-215v-180l-240-92-240 92v180q0 118.25 68 215t172 129Zm0-308Z"/></g>""");

    /// <summary>Material Symbols <c>extension</c>.</summary>
    public static readonly Glyph Integration =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-216h528v-205l57-11q17-4 28-17.39T840-480q0-17-11-30t-28-17l-57-11v-206H538l-11-57q-4-17-16.9-28-12.9-11-30.1-11-17 0-30.5 11T432-801l-10.75 57H216v113q44 21 70 62t26 89.09Q312-431 286-390t-70 62v112Zm0 72q-30 0-51-21t-21-51v-168q39 0 67.5-27.5T240-480q0-39-28.5-66.5T144-576.21V-744q0-29 21.15-50.5T216-816h145.64Q370-858 403-885t76.91-27Q523-912 556-885t41 69h147q29 0 50.5 21.5T816-744v147q42 8 69 41t27 75.96Q912-436 885-403q-27 33-69 41v146q0 30-21.5 51T744-144H216Zm312-336Z"/></g>""");

    /// <summary>Material Symbols <c>cloud</c>.</summary>
    public static readonly Glyph Storage =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M240-192q-80 0-136-56T48-384q0-76 52-131.5T227-576q23-85 92.5-138.5T480-768q103 0 179 69.5T744-528q70 0 119 49t49 119q0 70-49 119t-119 49H240Zm0-72h504q40 0 68-28t28-68q0-40-28-68t-68-28h-66l-6-65q-7-74-62-124.5T480-696q-64 0-115 38.5T297-556l-14 49-51 3q-48 3-80 37.5T120-384q0 50 35 85t85 35Zm240-216Z"/></g>""");

    /// <summary>Material Symbols <c>sync</c>.</summary>
    public static readonly Glyph Sync =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-192v-72h74q-45-40-71.5-95.5T192-480q0-101 61-177.5T408-758v75q-63 23-103.5 77.5T264-480q0 48 19.5 89t52.5 70v-63h72v192H216Zm336-10v-75q63-23 103.5-77.5T696-480q0-48-19.5-89T624-639v63h-72v-192h192v72h-74q45 40 71.5 95.5T768-480q0 101-61 177.5T552-202Z"/></g>""");

    /// <summary>Material Symbols <c>warning</c>.</summary>
    public static readonly Glyph Warning =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m48-144 432-720 432 720H48Zm127-72h610L480-724 175-216Zm330.5-58.29q10.5-10.29 10.5-25.5t-10.29-25.71q-10.29-10.5-25.5-10.5t-25.71 10.29q-10.5 10.29-10.5 25.5t10.29 25.71q10.29 10.5 25.5 10.5t25.71-10.29ZM444-384h72v-192h-72v192Zm36-86Z"/></g>""");

    /// <summary>Material Symbols <c>help</c>. The circled question every field's help and the
    /// door to the guide share.</summary>
    public static readonly Glyph Help =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M514-254q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14Zm-70-139h73q0-37 6.5-52.5T555-485q35-34 48.5-58t13.5-53q0-55-37.5-89.5T484-720q-51 0-88.5 27T343-620l65 27q9-28 28.5-43.5T482-652q28 0 46 16t18 42q0 23-15.5 41T496-518q-35 32-43.5 52.5T444-393Zm36 297q-79 0-149-30t-122.5-82.5Q156-261 126-331T96-480q0-80 30-149.5t82.5-122Q261-804 331-834t149-30q80 0 149.5 30t122 82.5Q804-699 834-629.5T864-480q0 79-30 149t-82.5 122.5Q699-156 629.5-126T480-96Zm0-72q130 0 221-91t91-221q0-130-91-221t-221-91q-130 0-221 91t-91 221q0 130 91 221t221 91Zm0-312Z"/></g>""");

    /// <summary>Material Symbols <c>confirmation_number</c>.</summary>
    public static readonly Glyph Tickets =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M479.79-312q15.21 0 25.71-10.29t10.5-25.5q0-15.21-10.29-25.71t-25.5-10.5q-15.21 0-25.71 10.29t-10.5 25.5q0 15.21 10.29 25.71t25.5 10.5Zm0-132q15.21 0 25.71-10.29t10.5-25.5q0-15.21-10.29-25.71t-25.5-10.5q-15.21 0-25.71 10.29t-10.5 25.5q0 15.21 10.29 25.71t25.5 10.5Zm0-132q15.21 0 25.71-10.29t10.5-25.5q0-15.21-10.29-25.71t-25.5-10.5q-15.21 0-25.71 10.29t-10.5 25.5q0 15.21 10.29 25.71t25.5 10.5ZM792-192H168q-29.7 0-50.85-21.15Q96-234.3 96-264v-144q29.7 0 50.85-21.21 21.15-21.21 21.15-51t-21.15-50.94Q125.7-552.3 96-552.3v-144q0-29.7 21.15-50.7 21.15-21 50.85-21h624q29.7 0 50.85 21.15Q864-725.7 864-696v144q-29.7 0-50.85 21.21-21.15 21.21-21.15 51t21.15 50.94Q834.3-407.7 864-407.7v144q0 29.7-21.15 50.7-21.15 21-50.85 21Zm0-72v-91q-32-19-52-52t-20-73q0-40 20-73t52-52v-91H168v91q32 19 52 52t20 73q0 40-20 73t-52 52v91h624ZM480-480Z"/></g>""");

    /// <summary>Busy indicator. An icon like any other, so it fits every slot that takes one
    /// (button, adornment, table cell) — the spin comes from the ns-spin rules in ns-mud.css.</summary>
    // Hand-drawn rather than a Symbols glyph: this one is a shape the CSS animates, so it
    // stays in the 0 0 24 24 space the host svg already provides — no transform to undo.
    public static readonly Glyph Progress =
        new("""<circle class="ns-spin" cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-dasharray="42 15"/>""");
}
