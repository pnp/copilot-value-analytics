import React from "react";
import { Button } from "@fluentui/react-components";
import { SurveyQuestionDTO } from "../../../apimodels/Models";
import { SurveyPage } from "../../../components/common/controls/SurveyPage";
import { ConfirmDialogue } from "../../../components/common/controls/ConfirmDialogue";
import { SurveyPageViewProps } from "./PropModels";

export const SurveyPageView: React.FC<SurveyPageViewProps> = (props) => {

  const [editMode, setEditMode] = React.useState<boolean>(false);
  const [deletePage, setDeletePage] = React.useState<boolean>(false);

  const onEditToggle = React.useCallback(() => {
    props.onStartEdit(props.page);
    setEditMode(!editMode);
  }, [editMode]);

  const onDelete = React.useCallback(() => {
    props.onDelete(props.page);
  }, [props.onDelete]);

  return (
    <div>
      <h2>{props.page.name} (
        {props.page.isPublished ? <span>Published</span> : <span>Draft</span>}
        )</h2>

      {deletePage &&
        <ConfirmDialogue title='Delete?' onCancel={() => setDeletePage(false)}
          onConfirm={onDelete}>
          <div>Are you sure you want to delete survey page '{props.page.name}'?</div>
        </ConfirmDialogue>
      }

      <div>
        <SurveyPage page={props.page} />
      </div>
      <div>
        {props.page.questions.length > 0 ?
          <>
            <p>Questions:</p>
            <ul>
              {props.page.questions.map((q: SurveyQuestionDTO, i: number) => (
                <li key={i}>{q.question}</li>
              ))}
            </ul>
          </>
          :
          <p>No questions defined for this page yet</p>
        }
      </div>

      <div className='nav'>
        <ul>
          <li>
            <Button appearance="primary" onClick={onEditToggle}>Edit</Button>
          </li>
          <li>
            <Button appearance="primary" onClick={() => setDeletePage(true)}>Delete</Button>
          </li>
        </ul>
      </div>
    </div>
  );
};
