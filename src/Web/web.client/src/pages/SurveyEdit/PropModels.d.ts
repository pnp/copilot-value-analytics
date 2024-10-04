
abstract interface PageProps {
    page: SurveyPageDB,
}


interface EditSurveyQuestionsProps extends PageProps {
    onQuestionEdited: Function,
    onQuestionDeleted: Function
}

interface SurveyPageViewProps extends PageProps {
    onStartEdit: Function,
    onDelete: Function
}

interface SurveyPageAndQuestionsEditProps extends PageProps {
    onPageEdited: Function,
    onPageSave: Function,
    onQuestionEdited: Function,
    onPageDeleted: Function,
    onQuestionDeleted: Function,
    onEditCancel: Function,
}

interface SurveyQuestionProps {
    q: SurveyQuestionDB;
    onQuestionEdited: Function;
    onQuestionDeleted: Function;
}

interface EditSurveyPageProps extends PageProps { onPageEdited: Function }
